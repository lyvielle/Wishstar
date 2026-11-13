using SkiaSharp;

namespace Wishstar.Services.Import {
    /// <summary>
    /// Finds the product photo in a screenshot without any ML: on a downsampled copy, pixels that differ from
    /// the page background (with all recognized text blanked out) are grouped into connected regions, and the
    /// largest photo-shaped region that is not covered in text wins. Buttons and bars are rejected by shape,
    /// icons and thumbnails by size.
    /// </summary>
    public static class PhotoLocator {
        private const int WorkSize = 420;
        private const int ColorThreshold = 34;   // sum of |dR|+|dG|+|dB| to count as "not background"
        private const double MinAreaFraction = 0.035;
        private const double MinAspect = 0.3, MaxAspect = 3.2;

        public static SKRectI? Locate(SKBitmap screenshot, IReadOnlyList<OcrLine> text) {
            float scale = Math.Min(1f, (float)WorkSize / Math.Max(screenshot.Width, screenshot.Height));
            int w = Math.Max(1, (int)(screenshot.Width * scale));
            int h = Math.Max(1, (int)(screenshot.Height * scale));

            using SKBitmap small = screenshot.Resize(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
                ?? throw new InvalidOperationException("Unable to downscale screenshot.");
            SKColor[] pixels = small.Pixels;

            SKColor background = DominantColor(pixels);
            var mask = new bool[w * h];
            for (int i = 0; i < pixels.Length; i++) {
                SKColor c = pixels[i];
                mask[i] = Math.Abs(c.Red - background.Red) + Math.Abs(c.Green - background.Green) + Math.Abs(c.Blue - background.Blue) > ColorThreshold;
            }

            // Text is not the photo: blank out every recognized line (slightly enlarged).
            var textMask = new bool[w * h];
            foreach (var line in text) {
                int pad = (int)Math.Ceiling(line.Height * 0.2 * scale) + 1;
                Fill(textMask, w, h, (int)(line.Box.Left * scale) - pad, (int)(line.Box.Top * scale) - pad,
                    (int)Math.Ceiling(line.Box.Right * scale) + pad, (int)Math.Ceiling(line.Box.Bottom * scale) + pad, true);
            }

            for (int i = 0; i < mask.Length; i++) {
                if (textMask[i]) {
                    mask[i] = false;
                }
            }

            bool[] openMask = (bool[])mask.Clone();

            // Close small gaps so a product made of several parts (or with thin outlines) becomes one region.
            int radius = Math.Max(2, w / 90);
            mask = Erode(Dilate(mask, w, h, radius), w, h, radius);

            var regions = Label(mask, w, h);
            double imageArea = (double)w * h;

            Region? best = null;
            double bestScore = 0;
            foreach (var region in regions) {
                int rw = region.Right - region.Left + 1, rh = region.Bottom - region.Top + 1;
                double area = (double)rw * rh;
                double aspect = (double)rw / rh;
                if (area < imageArea * MinAreaFraction || aspect < MinAspect || aspect > MaxAspect || area > imageArea * 0.92) {
                    continue;
                }

                double fill = region.Pixels / area;
                double textCoverage = CountInRect(textMask, w, region) / area;
                if (textCoverage > 0.35) {
                    continue; // a text block or banner, not a photo
                }

                double score = area * (0.4 + Math.Min(fill, 0.8)) * (1 - textCoverage * 1.5);
                if (score > bestScore) {
                    bestScore = score;
                    best = region;
                }
            }

            if (best == null) {
                return null;
            }

            // Pull in nearby parts of the same product (e.g. a shadow or a detached cable) that are smaller.
            var merged = best;
            int reach = (int)(Math.Max(w, h) * 0.03);
            foreach (var region in regions) {
                if (region == best || region.Pixels > best.Pixels) {
                    continue;
                }

                bool near = region.Left <= merged.Right + reach && region.Right >= merged.Left - reach
                    && region.Top <= merged.Bottom + reach && region.Bottom >= merged.Top - reach;
                int rw = region.Right - region.Left + 1, rh = region.Bottom - region.Top + 1;
                bool textHeavy = CountInRect(textMask, w, region) > rw * rh * 0.2;
                if (near && !textHeavy) {
                    merged = merged.Union(region);
                }
            }

            // Closing gaps can glue a neighbouring strip (e.g. the thumbnail column next to a gallery photo)
            // onto the photo: cut off narrow edge strips that are separated by a clean background gap.
            merged = TrimEdgeStrips(openMask, w, merged);

            // Back to screenshot pixels.
            float inv = 1f / scale;
            var rect = new SKRectI(
                Math.Max(0, (int)(merged.Left * inv)),
                Math.Max(0, (int)(merged.Top * inv)),
                Math.Min(screenshot.Width, (int)((merged.Right + 1) * inv)),
                Math.Min(screenshot.Height, (int)((merged.Bottom + 1) * inv)));
            if (rect.Width <= 16 || rect.Height <= 16) {
                return null;
            }

            // A product cut out on a plain backdrop gets a little breathing room; a full photo (busy edges)
            // is used exactly as found, otherwise the page around it would show up at the edges.
            using (var probe = new SKBitmap(rect.Width, rect.Height)) {
                screenshot.ExtractSubset(probe, rect);
                if (TryGetPlainBorder(probe, out _)) {
                    int padX = (int)(rect.Width * 0.04), padY = (int)(rect.Height * 0.04);
                    rect = new SKRectI(Math.Max(0, rect.Left - padX), Math.Max(0, rect.Top - padY),
                        Math.Min(screenshot.Width, rect.Right + padX), Math.Min(screenshot.Height, rect.Bottom + padY));
                }
            }

            return rect;
        }

        /// <summary>
        /// Crops the photo. When the crop has a plain border (product on a white/grey backdrop) it is padded
        /// with that color to the 4:3 card format, so the whole product stays visible on the wishlist.
        /// </summary>
        public static SKBitmap Crop(SKBitmap screenshot, SKRectI rect) {
            var cropped = new SKBitmap(rect.Width, rect.Height, screenshot.ColorType, screenshot.AlphaType);
            using (var canvas = new SKCanvas(cropped)) {
                canvas.DrawBitmap(screenshot, rect, new SKRect(0, 0, rect.Width, rect.Height));
            }

            if (!TryGetPlainBorder(cropped, out SKColor border)) {
                return cropped;
            }

            const double target = 4.0 / 3.0;
            double aspect = (double)cropped.Width / cropped.Height;
            int width = cropped.Width, height = cropped.Height;
            if (aspect < target) {
                width = (int)Math.Round(height * target);
            } else if (aspect > target * 1.05) {
                height = (int)Math.Round(width / target);
            } else {
                return cropped;
            }

            var padded = new SKBitmap(width, height, cropped.ColorType, cropped.AlphaType);
            using (var canvas = new SKCanvas(padded)) {
                canvas.Clear(border);
                canvas.DrawBitmap(cropped, (width - cropped.Width) / 2f, (height - cropped.Height) / 2f);
            }

            cropped.Dispose();
            return padded;
        }

        private static bool TryGetPlainBorder(SKBitmap bitmap, out SKColor color) {
            var samples = new List<SKColor>();
            int step = Math.Max(1, Math.Max(bitmap.Width, bitmap.Height) / 60);
            for (int x = 0; x < bitmap.Width; x += step) {
                samples.Add(bitmap.GetPixel(x, 0));
                samples.Add(bitmap.GetPixel(x, bitmap.Height - 1));
            }

            for (int y = 0; y < bitmap.Height; y += step) {
                samples.Add(bitmap.GetPixel(0, y));
                samples.Add(bitmap.GetPixel(bitmap.Width - 1, y));
            }

            // Plain when most of the border has one color; a shadow or a corner of the product may touch it.
            var dominant = samples.GroupBy(c => (c.Red >> 4, c.Green >> 4, c.Blue >> 4)).OrderByDescending(g => g.Count()).First().First();
            var close = samples.Where(c => Math.Abs(c.Red - dominant.Red) + Math.Abs(c.Green - dominant.Green) + Math.Abs(c.Blue - dominant.Blue) < 30).ToList();
            color = new SKColor((byte)close.Average(c => c.Red), (byte)close.Average(c => c.Green), (byte)close.Average(c => c.Blue));
            return close.Count >= samples.Count * 0.8;
        }

        private static SKColor DominantColor(SKColor[] pixels) {
            var counts = new Dictionary<int, int>();
            for (int i = 0; i < pixels.Length; i += 3) {
                SKColor c = pixels[i];
                int key = (c.Red >> 3) << 10 | (c.Green >> 3) << 5 | (c.Blue >> 3);
                counts[key] = counts.GetValueOrDefault(key) + 1;
            }

            int top = counts.MaxBy(kv => kv.Value).Key;
            return new SKColor((byte)(((top >> 10) & 31) << 3 | 4), (byte)(((top >> 5) & 31) << 3 | 4), (byte)((top & 31) << 3 | 4));
        }

        private sealed record Region(int Left, int Top, int Right, int Bottom, int Pixels) {
            public Region Union(Region other) => new(Math.Min(Left, other.Left), Math.Min(Top, other.Top),
                Math.Max(Right, other.Right), Math.Max(Bottom, other.Bottom), Pixels + other.Pixels);
        }

        private static Region TrimEdgeStrips(bool[] mask, int w, Region r) {
            const double MaxStrip = 0.3;
            for (int pass = 0; pass < 4; pass++) {
                int width = r.Right - r.Left + 1, height = r.Bottom - r.Top + 1;
                bool[] emptyColumns = new bool[width], emptyRows = new bool[height];
                for (int x = 0; x < width; x++) {
                    int count = 0;
                    for (int y = r.Top; y <= r.Bottom; y++) if (mask[y * w + r.Left + x]) count++;
                    emptyColumns[x] = count <= height * 0.02;
                }

                for (int y = 0; y < height; y++) {
                    int count = 0;
                    for (int x = r.Left; x <= r.Right; x++) if (mask[(r.Top + y) * w + x]) count++;
                    emptyRows[y] = count <= width * 0.02;
                }

                int cutLeft = FindStripCut(emptyColumns, MaxStrip, fromStart: true);
                int cutRight = FindStripCut(emptyColumns, MaxStrip, fromStart: false);
                int cutTop = FindStripCut(emptyRows, MaxStrip, fromStart: true);
                int cutBottom = FindStripCut(emptyRows, MaxStrip, fromStart: false);
                if (cutLeft + cutRight + cutTop + cutBottom == 0) {
                    break;
                }

                r = r with { Left = r.Left + cutLeft, Right = r.Right - cutRight, Top = r.Top + cutTop, Bottom = r.Bottom - cutBottom };
            }

            return r;
        }

        /// <summary>
        /// Returns how many lines to cut so that a content strip at the edge, followed by an empty gap, is removed
        /// (0 if there is none within <paramref name="maxFraction"/> of the size).
        /// </summary>
        private static int FindStripCut(bool[] empty, double maxFraction, bool fromStart) {
            int n = empty.Length, limit = (int)(n * maxFraction);
            int Index(int i) => fromStart ? i : n - 1 - i;

            int i = 0;
            while (i < limit && empty[Index(i)]) i++;          // leading empty lines (already background)
            int stripStart = i;
            while (i < limit && !empty[Index(i)]) i++;         // the strip itself
            if (i == stripStart || i >= limit) {
                return 0;
            }

            int gapStart = i;
            while (i < n && empty[Index(i)]) i++;              // the separating gap
            return i > gapStart && i < n ? i : 0;
        }

        private static List<Region> Label(bool[] mask, int w, int h) {
            var regions = new List<Region>();
            var seen = new bool[mask.Length];
            var queue = new Queue<int>();
            for (int start = 0; start < mask.Length; start++) {
                if (!mask[start] || seen[start]) {
                    continue;
                }

                int left = int.MaxValue, top = int.MaxValue, right = 0, bottom = 0, count = 0;
                seen[start] = true;
                queue.Enqueue(start);
                while (queue.Count > 0) {
                    int i = queue.Dequeue();
                    int x = i % w, y = i / w;
                    count++;
                    left = Math.Min(left, x); right = Math.Max(right, x);
                    top = Math.Min(top, y); bottom = Math.Max(bottom, y);

                    if (x > 0) Visit(i - 1);
                    if (x < w - 1) Visit(i + 1);
                    if (y > 0) Visit(i - w);
                    if (y < h - 1) Visit(i + w);
                }

                regions.Add(new Region(left, top, right, bottom, count));
            }

            return regions;

            void Visit(int n) {
                if (mask[n] && !seen[n]) {
                    seen[n] = true;
                    queue.Enqueue(n);
                }
            }
        }

        // Separable square dilation/erosion using running sums (fast enough at this resolution).
        private static bool[] Dilate(bool[] src, int w, int h, int r) => Morph(src, w, h, r, dilate: true);
        private static bool[] Erode(bool[] src, int w, int h, int r) => Morph(src, w, h, r, dilate: false);

        private static bool[] Morph(bool[] src, int w, int h, int r, bool dilate) {
            var tmp = new bool[src.Length];
            var dst = new bool[src.Length];
            for (int y = 0; y < h; y++) {
                for (int x = 0; x < w; x++) {
                    bool v = !dilate;
                    for (int k = Math.Max(0, x - r); k <= Math.Min(w - 1, x + r); k++) {
                        if (src[y * w + k] == dilate) { v = dilate; break; }
                    }
                    tmp[y * w + x] = v;
                }
            }

            for (int x = 0; x < w; x++) {
                for (int y = 0; y < h; y++) {
                    bool v = !dilate;
                    for (int k = Math.Max(0, y - r); k <= Math.Min(h - 1, y + r); k++) {
                        if (tmp[k * w + x] == dilate) { v = dilate; break; }
                    }
                    dst[y * w + x] = v;
                }
            }

            return dst;
        }

        private static void Fill(bool[] mask, int w, int h, int x0, int y0, int x1, int y1, bool value) {
            for (int y = Math.Max(0, y0); y < Math.Min(h, y1); y++) {
                for (int x = Math.Max(0, x0); x < Math.Min(w, x1); x++) {
                    mask[y * w + x] = value;
                }
            }
        }

        private static int CountInRect(bool[] mask, int w, Region r) {
            int count = 0;
            for (int y = r.Top; y <= r.Bottom; y++) {
                for (int x = r.Left; x <= r.Right; x++) {
                    if (mask[y * w + x]) count++;
                }
            }

            return count;
        }
    }
}
