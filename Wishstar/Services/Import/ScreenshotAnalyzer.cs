using System.Text.RegularExpressions;
using SkiaSharp;
using Wishstar.Models;

namespace Wishstar.Services.Import {
    public sealed record ScreenshotAnalysis(
        string? Name,
        string? FullTitle,
        double? Price,
        CurrencyType? Currency,
        SKRectI? PhotoRect,
        IReadOnlyList<OcrLine> Lines);

    /// <summary>Extracts title, price and product photo position from a shop screenshot.</summary>
    public static partial class ScreenshotAnalyzer {
        // Prices that are not what you'd pay for the item itself.
        [GeneratedRegex(@"\buvp\b|statt|vorher|ursprünglich|\bwar\b|\bwas\b|\brrp\b|list ?price|listenpreis|typical price|versand|shipping|delivery|lieferung|/\s?(?:monat|mo|month|mtl|kg|100|l|m|stk)\b|per month|pro monat|monatlich|\bmtl\b|\braten?\b|finanzierung|coupon|gutschein|\bsave\b|\bspare|sparen|ersparnis|rabatt|discount|grundpreis|unit price|\bzzgl|\bab\b|\bfrom\b|\bover\b|\bunder\b|\bbis\b|\bmin\.?\b|mindestbestell|gratis|kostenlos|free",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex NotThePriceRegex();

        private sealed record PriceCandidate(double Amount, CurrencyType? Currency, double Score, OcrLine Line, int SuperscriptStartX);

        public static async Task<ScreenshotAnalysis> AnalyzeAsync(SKBitmap screenshot, CancellationToken cancellationToken) {
            IReadOnlyList<OcrLine> lines = await OcrEngine.RecognizeAsync(screenshot, cancellationToken);

            // The title is searched next to the most prominent price, then the price is chosen again with the
            // title known (prices next to the title belong to the product, not to recommendations).
            PriceCandidate? anchor = SelectPrice(screenshot, lines, null);
            TitleCandidate? title = TitleExtractor.FindTitle(lines, screenshot.Height, anchor?.Line.Box);
            (double Amount, CurrencyType? Currency)? price = await ResolvePriceAsync(screenshot, lines, SelectPrice(screenshot, lines, title), cancellationToken);
            SKRectI? photo = PhotoLocator.Locate(screenshot, lines);

            string? fullTitle = title?.Text;
            string? name = fullTitle != null ? TitleExtractor.Shorten(fullTitle) : null;
            return new ScreenshotAnalysis(string.IsNullOrWhiteSpace(name) ? null : name, fullTitle, price?.Amount, price?.Currency, photo, lines);
        }

        private static PriceCandidate? SelectPrice(SKBitmap screenshot, IReadOnlyList<OcrLine> lines, TitleCandidate? title) {
            double medianHeight = TitleExtractor.Median(lines.Select(l => (double)l.Height));
            PriceCandidate? best = null;

            foreach (OcrLine line in lines) {
                var matches = PriceParser.FindAll(line.Text).ToList();
                bool bare = false;
                if (matches.Count == 0 && PriceParser.ParseBareAmount(line.Text) is PriceMatch bareMatch) {
                    matches.Add(bareMatch);
                    bare = true;
                }

                if (matches.Count == 0) {
                    continue;
                }

                // Labels like "UVP:" or "Versand:" are often recognized as a separate line just left of the price.
                string context = line.Text + " " + string.Join(' ', lines
                    .Where(o => o != line && Math.Abs(o.CenterY - line.CenterY) < line.Height * 0.6 && o.Box.Right <= line.Box.Left + 4
                        && line.Box.Left - o.Box.Right < line.Height * 3)
                    .Select(o => o.Text));

                // Only the first price of a line counts: in "45,00 € 60,00 €" the second one is the old price.
                PriceMatch match = matches[0];
                // "€279⁹⁹": OCR merges superscript cents into the number (and sometimes loses a digit doing so).
                int superscriptStart = -1;
                if (match.CanHaveSuperscriptCents && TrailingRaisedGlyphs(screenshot, line.Box) is { Count: 1 or 2 } raised) {
                    superscriptStart = raised.StartX;
                }

                double score = Math.Pow(line.Height / medianHeight, 1.5);
                if (NotThePriceRegex().IsMatch(context)) {
                    score *= 0.12;
                }

                if (IsStruckThrough(screenshot, line.Box)) {
                    score *= 0.08;
                }

                if (line.Text.Length > 24) {
                    score *= 0.6; // the real price usually stands on its own line
                }

                if (matches.Count > 1) {
                    score *= 0.8;
                }

                if (bare) {
                    score *= 0.6;
                }

                // Prices close to the title belong to the product (not to recommendations further down).
                if (title != null) {
                    double distance = Math.Max(0, Math.Max(title.Box.Top - line.Box.Bottom, line.Box.Top - title.Box.Bottom));
                    score *= 1 + 0.8 * Math.Exp(-distance / (medianHeight * 6));
                }

                if (best == null || score > best.Score + 1e-9) {
                    best = new PriceCandidate(match.Amount, match.Currency, score, line, superscriptStart);
                }
            }

            return best;
        }

        private static async Task<(double Amount, CurrencyType? Currency)?> ResolvePriceAsync(SKBitmap screenshot, IReadOnlyList<OcrLine> lines,
            PriceCandidate? best, CancellationToken cancellationToken) {
            if (best is not { } result) {
                return null;
            }

            double amount = result.Amount;
            if (result.SuperscriptStartX > 0) {
                amount = await ReadSuperscriptPriceAsync(screenshot, result.Line.Box, result.SuperscriptStartX, cancellationToken)
                    ?? result.Amount / 100;
            }

            // The symbol was not recognized: use the currency the other prices on the page are in, if any.
            CurrencyType? currency = result.Currency ?? lines
                .SelectMany(l => PriceParser.FindAll(l.Text))
                .Where(m => m.Currency != null)
                .GroupBy(m => m.Currency)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();

            return (Math.Round(amount, 2), currency);
        }

        /// <summary>Reads "€279⁹⁹" by recognizing the main number and the raised cents separately.</summary>
        private static async Task<double?> ReadSuperscriptPriceAsync(SKBitmap screenshot, SKRectI box, int centsStartX, CancellationToken cancellationToken) {
            SKColor background = BorderColor(screenshot, box);
            string main = await RecognizeCropAsync(screenshot, new SKRectI(box.Left, box.Top, centsStartX - 1, box.Bottom), background, cancellationToken);
            string cents = await RecognizeCropAsync(screenshot, new SKRectI(centsStartX, box.Top, box.Right, box.Bottom), background, cancellationToken);

            Match integer = Regex.Match(main, @"(\d{1,3}(?:[.,'’\u00A0\u202F ]\d{3})+|\d+)(?!.*\d)");
            string centDigits = new([.. cents.Where(char.IsDigit)]);
            if (!integer.Success || centDigits.Length != 2) {
                return null;
            }

            string digits = new([.. integer.Value.Where(char.IsDigit)]);
            return double.Parse(digits, System.Globalization.CultureInfo.InvariantCulture) + int.Parse(centDigits) / 100.0;
        }

        private static async Task<string> RecognizeCropAsync(SKBitmap screenshot, SKRectI rect, SKColor background, CancellationToken cancellationToken) {
            rect.Intersect(new SKRectI(0, 0, screenshot.Width, screenshot.Height));
            if (rect.Width < 2 || rect.Height < 2) {
                return string.Empty;
            }

            // Upscale and give the detector some margin around the glyphs.
            float scale = Math.Max(1f, 64f / rect.Height);
            int pad = (int)(rect.Height * scale);
            using var crop = new SKBitmap((int)(rect.Width * scale) + pad * 2, (int)(rect.Height * scale) + pad * 2);
            using (var canvas = new SKCanvas(crop)) {
                canvas.Clear(background);
                canvas.DrawBitmap(screenshot, rect, SKRect.Create(pad, pad, rect.Width * scale, rect.Height * scale));
            }

            var lines = await OcrEngine.RecognizeAsync(crop, cancellationToken);
            return string.Join(' ', lines.OrderBy(l => l.Box.Left).Select(l => l.Text));
        }

        /// <summary>
        /// Counts glyphs at the end of a text box that sit clearly above the baseline – superscript cents as
        /// in "€279⁹⁹", which OCR reads as one number.
        /// </summary>
        private static (int Count, int StartX) TrailingRaisedGlyphs(SKBitmap bitmap, SKRectI box) {
            if (box.Width < 10 || box.Height < 8) {
                return (0, 0);
            }

            SKColor background = BorderColor(bitmap, box);
            var glyphs = new List<(int Left, int Top, int Bottom)>();
            int glyphLeft = 0, glyphTop = int.MaxValue, glyphBottom = -1;
            bool inGlyph = false;
            for (int x = Math.Max(0, box.Left); x < Math.Min(bitmap.Width, box.Right); x++) {
                int top = int.MaxValue, bottom = -1;
                for (int y = Math.Max(0, box.Top); y < Math.Min(bitmap.Height, box.Bottom); y++) {
                    if (IsInk(bitmap.GetPixel(x, y), background)) {
                        top = Math.Min(top, y);
                        bottom = y;
                    }
                }

                if (bottom >= 0) {
                    if (!inGlyph) {
                        glyphLeft = x;
                    }

                    glyphTop = Math.Min(glyphTop, top);
                    glyphBottom = Math.Max(glyphBottom, bottom);
                    inGlyph = true;
                } else if (inGlyph) {
                    glyphs.Add((glyphLeft, glyphTop, glyphBottom));
                    glyphTop = int.MaxValue;
                    glyphBottom = -1;
                    inGlyph = false;
                }
            }

            if (inGlyph) {
                glyphs.Add((glyphLeft, glyphTop, glyphBottom));
            }

            if (glyphs.Count < 3) {
                return (0, 0);
            }

            // Compare the last one or two glyphs with the glyph just before them: superscript cents end clearly
            // higher and are smaller. (The box itself may reach into neighbouring lines, so no global baseline.)
            int raised = 0, startX = 0;
            for (int run = 2; run >= 1 && raised == 0; run--) {
                if (glyphs.Count < run + 2) {
                    continue;
                }

                var reference = glyphs[glyphs.Count - run - 1];
                int referenceHeight = reference.Bottom - reference.Top;
                var tail = glyphs.Skip(glyphs.Count - run).ToList();
                if (tail.All(g => g.Bottom < reference.Bottom - referenceHeight * 0.3 && g.Bottom - g.Top < referenceHeight * 0.75)) {
                    raised = run;
                    startX = tail[0].Left;
                }
            }

            return (raised, startX);
        }

        private static SKColor BorderColor(SKBitmap bitmap, SKRectI box) {
            var samples = new List<SKColor>();
            for (int x = Math.Max(0, box.Left); x < Math.Min(bitmap.Width, box.Right); x += 2) {
                samples.Add(bitmap.GetPixel(x, Math.Clamp(box.Top, 0, bitmap.Height - 1)));
                samples.Add(bitmap.GetPixel(x, Math.Clamp(box.Bottom - 1, 0, bitmap.Height - 1)));
            }

            return samples.GroupBy(c => (c.Red >> 4, c.Green >> 4, c.Blue >> 4)).OrderByDescending(g => g.Count()).First().First();
        }

        private static bool IsInk(SKColor c, SKColor background) {
            return Math.Abs(c.Red - background.Red) + Math.Abs(c.Green - background.Green) + Math.Abs(c.Blue - background.Blue) > 90;
        }

        /// <summary>Detects a strike-through line: an unbroken horizontal ink run through the middle of the text.</summary>
        private static bool IsStruckThrough(SKBitmap bitmap, SKRectI box) {
            if (box.Width < 8 || box.Height < 6) {
                return false;
            }

            SKColor background = BorderColor(bitmap, box);
            int bestRun = 0;
            for (int y = box.Top + box.Height * 35 / 100; y <= box.Top + box.Height * 70 / 100; y++) {
                if (y < 0 || y >= bitmap.Height) {
                    continue;
                }

                int run = 0;
                for (int x = Math.Max(0, box.Left); x < Math.Min(bitmap.Width, box.Right); x++) {
                    run = IsInk(bitmap.GetPixel(x, y), background) ? run + 1 : 0;
                    bestRun = Math.Max(bestRun, run);
                }
            }

            return bestRun > box.Width * 0.7;
        }
    }
}
