using System;
using System.IO;
using Tizen.NUI;
using Path = System.IO.Path;

namespace BetterTizen.Diagnostics
{
    // Call Start on the NUI/UI thread after views and artwork have finished loading.
    // Keep the returned request alive until completion. It captures this app's scene,
    // not Samsung's compositor or another app; hardware video planes are excluded.
    public sealed class OwnWindowCapture : IDisposable
    {
        private Capture capture;
        private readonly string path;
        private readonly Action<byte[], string> complete;
        private bool finished;

        private OwnWindowCapture(string path, Action<byte[], string> complete)
        {
            this.path = path;
            this.complete = complete;
        }

        public static OwnWindowCapture Start(Window window, string privatePngPath,
            Action<byte[], string> complete, int width = 1920, int height = 1080)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));
            if (complete == null) throw new ArgumentNullException(nameof(complete));
            if (String.IsNullOrEmpty(privatePngPath) || !Path.IsPathRooted(privatePngPath))
                throw new ArgumentException("Use an absolute app-private PNG path.", nameof(privatePngPath));
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            Directory.CreateDirectory(Path.GetDirectoryName(privatePngPath));
            var request = new OwnWindowCapture(privatePngPath, complete);
            try
            {
                request.capture = new Capture();
                request.capture.IsExclusive = false;
                request.capture.Finished += request.OnFinished;
                request.capture.Start(window.GetDefaultLayer(), new Size(width, height),
                    privatePngPath, window.BackgroundColor);
                return request;
            }
            catch
            {
                request.Dispose();
                throw;
            }
        }

        private void OnFinished(object sender, CaptureFinishedEventArgs args)
        {
            if (finished) return;
            finished = true;
            if (!args.Success) { complete(null, "Dali scene capture failed."); return; }
            byte[] png;
            try
            {
                png = File.ReadAllBytes(path);
                if (png.Length < 24 || png[0] != 137 || png[1] != 80 || png[2] != 78 || png[3] != 71)
                    throw new IOException("Capture did not produce a PNG.");
            }
            catch (Exception error) { complete(null, error.GetType().Name + ": " + error.Message); return; }
            complete(png, null);
            // Caller can upload bytes off-thread. Dispose on the UI thread after this
            // callback has returned, so native signal dispatch is not torn down inside it.
        }

        public void Dispose()
        {
            if (capture == null) return;
            capture.Finished -= OnFinished;
            capture.Dispose();
            capture = null;
        }
    }
}
