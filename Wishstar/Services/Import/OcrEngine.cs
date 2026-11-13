using RapidOcrNet;
using SkiaSharp;

namespace Wishstar.Services.Import {
    /// <summary>One recognized line of text and its bounding box in screenshot pixels.</summary>
    public sealed record OcrLine(string Text, SKRectI Box, float Score) {
        public int Height => Box.Height;
        public float CenterY => Box.MidY;
    }

    /// <summary>
    /// Local OCR (PaddleOCR v5 Latin models via ONNX Runtime, bundled with RapidOcrNet; no external service).
    /// The engine is loaded on first use, handles one screenshot at a time (keeps memory predictable on a
    /// small server) and is unloaded again after a few idle minutes.
    /// </summary>
    public static class OcrEngine {
        private static readonly TimeSpan IdleUnloadAfter = TimeSpan.FromMinutes(10);
        private static readonly SemaphoreSlim _Gate = new(1, 1);
        private static readonly RapidOcrOptions _Options = RapidOcrOptions.Default with {
            // Screenshots are large; allow the detector to work at a higher resolution than the default so
            // small print (prices, labels) is still found.
            MaxSideLen = 2048
        };

        private static RapidOcr? _Engine;
        private static Timer? _UnloadTimer;

        public static async Task<IReadOnlyList<OcrLine>> RecognizeAsync(SKBitmap bitmap, CancellationToken cancellationToken) {
            await _Gate.WaitAsync(cancellationToken);
            try {
                _UnloadTimer?.Dispose();
                _UnloadTimer = null;

                if (_Engine == null) {
                    // The bundled models are copied next to the app; resolve them independently of the working directory.
                    string Model(string file) => Path.Combine(AppContext.BaseDirectory, RapidOcr.ModelsFolderName, RapidOcr.ModelsVersion, Path.GetFileName(file));
                    var engine = new RapidOcr();
                    var sessionOptions = RapidOcr.GetDefaultSessionOptions(Math.Clamp(Environment.ProcessorCount, 1, 4));
                    // Without the arena, ONNX Runtime returns memory after each screenshot instead of keeping the
                    // largest buffers it ever needed (every screenshot size differs) – important on a small server.
                    sessionOptions.EnableCpuMemArena = false;
                    sessionOptions.EnableMemoryPattern = false;
                    engine.InitModels(Model(RapidOcr.DefaultDetModelPath), Model(RapidOcr.DefaultClsModelPath),
                        Model(RapidOcr.DefaultRecModelPath), Model(RapidOcr.DefaultKeysFilePath), sessionOptions);
                    _Engine = engine;
                }

                OcrResult result = await _Engine.DetectAsync(bitmap, _Options, cancellationToken);
                return [.. result.TextBlocks
                    .Where(b => !string.IsNullOrWhiteSpace(b.Text) && b.BoxPoints is { Length: 4 })
                    .Select(b => new OcrLine(b.Text.Trim(), ToRect(b.BoxPoints), b.CharScores is { Length: > 0 } s ? s.Average() : b.BoxScore))];
            } finally {
                _UnloadTimer = new Timer(_ => Unload(), null, IdleUnloadAfter, Timeout.InfiniteTimeSpan);
                _Gate.Release();
            }
        }

        private static void Unload() {
            if (!_Gate.Wait(0)) {
                return; // Busy: the next request re-arms the timer.
            }

            try {
                _Engine?.Dispose();
                _Engine = null;
            } finally {
                _Gate.Release();
            }
        }

        private static SKRectI ToRect(SKPointI[] points) {
            return new SKRectI(points.Min(p => p.X), points.Min(p => p.Y), points.Max(p => p.X), points.Max(p => p.Y));
        }
    }
}
