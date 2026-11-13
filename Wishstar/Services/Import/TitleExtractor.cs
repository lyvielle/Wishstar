using System.Text.RegularExpressions;
using SkiaSharp;

namespace Wishstar.Services.Import {
    public sealed record TitleCandidate(string Text, SKRectI Box, double Score);

    /// <summary>
    /// Picks the product title among the recognized lines and shortens it. Product titles are the most
    /// prominent multi-word text that is not shop UI (buttons, ratings, delivery info, prices, navigation).
    /// </summary>
    public static partial class TitleExtractor {
        private const int MaxShortWords = 6;
        private const int MaxShortLength = 60;

        // Shop UI phrases that are never part of a product title (checked on short lines only).
        [GeneratedRegex(@"warenkorb|einkaufswagen|\b(?:jetzt |sofort-?)?kaufen\b|\bbuy\b|add to|\bbag\b|checkout|\bjetzt\b|sofort|preisvorschlag|besuche|visit the|bewertung|\brating|review|sterne|\bstars?\b|von 5|out of 5|auf lager|in stock|lieferung|delivery|versand|shipping|\bships?\b|inkl\.|incl\.|mwst|\bust\b|\bvat\b|\btax\b|\bsuche\b|\bsearch\b|\bfinden\b|anmelden|sign in|log ?in|konto|account|hallo|\bmen[uü]\b|teilen|\bshare\b|^\s*(?:gr[öo]ß?e|size|farbe|colou?r)\b|w[äa]hlen|\bselect\b|verf[üu]gbar|available|gekauft|bought|angebote|kundenservice|customer service|\bmodel\b|\bsku\b|nur noch|only \d|verk[äa]ufer|seller|\bsold\b|verkauft|refund|erstattung|sponsored|gesponsert|rabatt|discount|\bsave\b|sparen|coupon|gutschein|wunschliste|wishlist|merken|favorit|zur[üu]ck|\bback\b|startseite|\bhome\b|kategorien|categories|\bfilter\b|sortieren|\bsort\b|cookie|alle preise|all prices|\bweitere\b|\bmore\b|\bdetails\b|beschreibung|description|produktinfo|\bneu\b|\bnew\b|\bsale\b|%",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex ShopUiRegex();

        // Wording that marks even a long line as shop UI (stock notices, search prompts, delivery promises...).
        [GeneratedRegex(@"nur noch|\bsold\b|verkauft|refund|erstattung|import charges|einfuhr|importgeb|\bincluded\b|inbegriffen|eligible|dispatched|versandt|order now|jetzt bestellen|verf[üu]gbar|available|warenk[öo]rb|einkaufswagen|\bcart\b|\bbasket\b|\bkaufen\b|\bbuy\b|lieferung|delivery|versand|shipping|bewertung|reviews?\b|ratings?\b|sterne|gekauft|bought|besuche|visit the|angebot|\bhelp\b|\bfind\b|\bsuche\b|\bsearch\b|anmelden|sign in|r[üu]ckgabe|returns?\b|garantie|warranty|inkl\.|incl\.|zzgl", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex StrongUiRegex();

        [GeneratedRegex(@"\?\s*$|^\d{1,2}[:.]\d{2}\b|www\.|https?:|\b[a-z0-9-]+\.(com|de|at|ch|co\.uk|net|org|shop|eu|fr|it|es|nl|jp)\b|[★☆]|\*{3,}|(?:\s[>›»/]\s[^>›»]+){2,}|^\W*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex NoiseRegex();

        // Badges some shops put in front of the title on the same line, e.g. "Fastest delivery: 5 BUSINESS DAYS".
        [GeneratedRegex(@"^(?:\w+\s){0,2}(?:delivery|lieferung|versand|shipping)\s*:?\s*\d+(?:\s*[-–]\s*\d+)?\s*(?:business\s+|working\s+|werk)?(?:days?|tage[n]?|hours?|stunden)\b\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex UiPrefixRegex();

        // Leading audience words carry little information on a personal wishlist.
        [GeneratedRegex(@"^(?:women'?s|men'?s|ladies|girls'?|boys'?|kids'?|unisex|damen|herren|kinder|mädchen|jungen)\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex AudiencePrefixRegex();

        // A title cut off by the shop ("Mid Block Hee…"): the last word is incomplete.
        [GeneratedRegex(@"\s*\S*(?:\.{2,}|…)\s*$")]
        private static partial Regex TruncatedEndRegex();

        [GeneratedRegex(@"(?<=\p{L})'S\b")]
        private static partial Regex ShoutedPossessiveRegex();

        [GeneratedRegex(@"\s*(?:\||·|•|–|—|;|:\s|,\s|\s-\s|\s\()\s*")]
        private static partial Regex SeparatorRegex();

        // Specs that make names long but are rarely needed in a short name: 128GB, 350ml, 77x147 cm, 2er Pack ...
        [GeneratedRegex(@"\b\d+(?:[.,]\d+)?\s?(?:gb|tb|mb|ml|cl|l|g|kg|mg|mm|cm|m|zoll|inch|""|w|v|mah|hz|stk|st[üu]ck|pcs|pack|teilig)\b|\b\d+\s?x\s?\d+(?:\s?x\s?\d+)?(?:\s?(?:cm|mm|m))?\b|\b\d+er[- ]?(?:set|pack)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex SpecRegex();

        /// <param name="priceBox">Where the most likely price is: shops place the title right next to it.</param>
        public static TitleCandidate? FindTitle(IReadOnlyList<OcrLine> lines, int imageHeight, SKRectI? priceBox = null) {
            if (lines.Count == 0) {
                return null;
            }

            double medianHeight = Median(lines.Select(l => (double)l.Height));
            var cleaned = lines
                .Select(l => l with { Text = Clean(UiPrefixRegex().Replace(l.Text, string.Empty)) })
                .OrderBy(l => l.Box.Top)
                .ToList();
            var candidates = cleaned.Where(l => IsTitleCandidate(l, allowUiWords: false)).ToList();

            // Group consecutive lines of the same size and alignment into paragraphs (multi-line titles).
            // A short line with a UI-ish word ("Home Dress Set") may continue a title, but never start one.
            var paragraphs = new List<List<OcrLine>>();
            foreach (var line in cleaned.Where(l => IsTitleCandidate(l, allowUiWords: true))) {
                var target = paragraphs.FirstOrDefault(p => Continues(p, line));
                if (target != null) {
                    target.Add(line);
                } else if (candidates.Contains(line)) {
                    paragraphs.Add([line]);
                }
            }

            TitleCandidate? best = null;
            foreach (var paragraph in paragraphs) {
                string text = Clean(TruncatedEndRegex().Replace(string.Join(' ', paragraph.Select(l => l.Text)), string.Empty));
                SKRectI box = Union(paragraph.Select(l => l.Box));
                double lineHeight = paragraph.Average(l => l.Height);
                int words = CountWords(text);

                double lengthFactor = words switch {
                    < 2 => 0.25,
                    2 => 0.7,
                    _ when text.Length > 320 => 0.35,
                    _ when text.Length > 180 => 0.75,
                    _ => 1.0
                };

                // Boxes spanning two lines of text look like huge print; don't let size dominate everything else.
                double relativeSize = Math.Min(lineHeight / medianHeight, 2.2);
                double positionFactor = box.Top < imageHeight * 0.05 ? 0.4 : 1.0;
                double confidence = Math.Pow(paragraph.Average(l => l.Score), 3);
                int uiHits = StrongUiRegex().Matches(text).Count;
                double uiFactor = uiHits switch { 0 => 1.0, 1 => 0.45, _ => 0.15 };
                // Most shops show the title above the price; some (Etsy) below it.
                double priceFactor = priceBox is SKRectI price ? PricePosition(box, price, lineHeight) switch {
                    > 0 => 2.5,
                    < 0 => 1.5,
                    _ => 1.0
                } : 1.0;
                double score = Math.Pow(relativeSize, 1.6) * lengthFactor * positionFactor * confidence * uiFactor * priceFactor;

                if (best == null || score > best.Score) {
                    best = new TitleCandidate(text, box, score);
                }
            }

            if (best == null) {
                return null;
            }

            // A short line right above the title is usually the brand or product line ("Nike", "KALLAX").
            var above = candidates
                .Where(l => l.Box.Bottom <= best.Box.Top + 2
                    && best.Box.Top - l.Box.Bottom <= Math.Max(l.Height, 12) * 1.2
                    && Math.Abs(l.Box.Left - best.Box.Left) <= Math.Max(l.Height, 12) * 2
                    && CountWords(l.Text) <= 3)
                .OrderByDescending(l => l.Box.Bottom)
                .FirstOrDefault();
            if (above != null && !best.Text.StartsWith(above.Text, StringComparison.OrdinalIgnoreCase)) {
                best = best with { Text = Clean(above.Text + " " + best.Text), Box = Union([above.Box, best.Box]) };
            }

            return best;
        }

        /// <summary>
        /// Shortens a shop title to a readable wish name, e.g.
        /// "Sony WH-1000XM5 Kabellose Bluetooth Noise Cancelling Kopfhörer, 30 Stunden Akkulaufzeit, ..." →
        /// "Sony WH-1000XM5 Kabellose Bluetooth Kopfhörer".
        /// </summary>
        public static string Shorten(string title) {
            string text = Clean(title);
            string[] segments = [.. SeparatorRegex().Split(text).Select(s => s.Trim(' ', '(', ')', '-', ',')).Where(s => s.Length > 0)];
            if (segments.Length == 0) {
                return text;
            }

            // Take the first segment; if it is just a brand ("Apple - MacBook Air ..."), add the next one.
            string name = segments[0];
            for (int i = 1; i < segments.Length && (CountWords(name) < 2 || name.Length < 8); i++) {
                name += " " + segments[i];
            }

            string withoutSpecs = Clean(SpecRegex().Replace(name, " "));
            if (CountWords(withoutSpecs) >= 2) {
                name = withoutSpecs;
            }

            // Drop an audience word at the start or right after the brand ("LUXMAX Women's Kawaii ...").
            string[] parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < Math.Min(2, parts.Length); i++) {
                if (AudiencePrefixRegex().IsMatch(parts[i] + " ") && parts.Length >= 4) {
                    name = string.Join(' ', parts.Where((_, index) => index != i));
                    break;
                }
            }

            string[] words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > MaxShortWords) {
                // Keep the beginning (brand, model, material) and the end, which usually names the product type
                // ("Women's Winter Warm … Home Dress Set").
                words = [.. words.Take(MaxShortWords / 2), .. words.TakeLast(MaxShortWords / 2)];
            }

            name = string.Join(' ', words);
            while (name.Length > MaxShortLength && name.Contains(' ')) {
                name = name[..name.LastIndexOf(' ')];
            }

            return name.Trim(' ', ',', '-', '|', ':', ';');
        }

        public static string Clean(string text) {
            string cleaned = string.Join(' ', text.Replace(' ', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries));
            cleaned = ShoutedPossessiveRegex().Replace(cleaned, "'s"); // "Women'S" → "Women's"
            return cleaned.Trim(' ', ',', '-', '|', ':', ';', '·', '•');
        }

        /// <summary>1 if the text is right above the price, -1 if right below it, 0 otherwise.</summary>
        private static int PricePosition(SKRectI title, SKRectI price, double lineHeight) {
            bool aligned = Math.Abs(title.Left - price.Left) <= lineHeight * 3
                || (Math.Min(title.Right, price.Right) - Math.Max(title.Left, price.Left)) > 0;
            if (!aligned) {
                return 0;
            }

            if (title.Bottom <= price.Top + lineHeight && price.Top - title.Bottom <= lineHeight * 5) {
                return 1;
            }

            return title.Top >= price.Bottom - lineHeight && title.Top - price.Bottom <= lineHeight * 4 ? -1 : 0;
        }

        private static bool IsTitleCandidate(OcrLine line, bool allowUiWords) {
            string text = line.Text.Trim();
            if (line.Score < 0.75 || text.Length == 0) {
                return false; // OCR guesses (e.g. "Cik Colect RE") are worse than no title
            }

            int letters = text.Count(char.IsLetter);
            int visible = text.Count(c => !char.IsWhiteSpace(c));
            if (letters < 3 || visible == 0 || letters < visible * 0.5) {
                return false;
            }

            if (PriceParser.FindAll(text).Any() || NoiseRegex().IsMatch(text)) {
                return false;
            }

            // Shop UI wording only disqualifies short lines; long titles may contain words like "für".
            if (allowUiWords && ShopUiRegex().IsMatch(text) && CountWords(text) <= 6) {
                // Continuation lines like "Home Dress Set": short, plain words only (no seller/rating lines).
                return CountWords(text) <= 4 && !text.Any(char.IsDigit) && !StrongUiRegex().IsMatch(text) && !text.Contains('·');
            }

            return CountWords(text) > 6 || !ShopUiRegex().IsMatch(text);
        }

        private static bool Continues(List<OcrLine> paragraph, OcrLine line) {
            OcrLine last = paragraph[^1];
            double height = paragraph.Average(l => l.Height);
            bool sameSize = Math.Abs(line.Height - height) <= height * 0.25;
            bool close = line.Box.Top - last.Box.Bottom <= height * 0.9 && line.Box.Top >= last.Box.Top;
            // Left-aligned or centered under each other (merely overlapping lines can be unrelated, e.g. a search field).
            bool aligned = Math.Abs(line.Box.Left - last.Box.Left) <= height * 1.5
                || Math.Abs(line.Box.MidX - last.Box.MidX) <= height * 1.5;
            return sameSize && close && aligned;
        }

        private static int CountWords(string text) => text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        private static SKRectI Union(IEnumerable<SKRectI> boxes) {
            var list = boxes.ToList();
            return new SKRectI(list.Min(b => b.Left), list.Min(b => b.Top), list.Max(b => b.Right), list.Max(b => b.Bottom));
        }

        internal static double Median(IEnumerable<double> values) {
            var sorted = values.OrderBy(v => v).ToList();
            return sorted.Count == 0 ? 1 : Math.Max(1, sorted[sorted.Count / 2]);
        }
    }
}
