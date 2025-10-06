using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;

namespace ColorVision.UI.Nets
{
    public class ProgressableStreamContent : HttpContent
    {
        private const int defaultBufferSize = 4096;
        private readonly Stream content;
        private readonly int bufferSize;
        private readonly Action<double, string, string> progress;
        private Stopwatch stopwatch;

        public ProgressableStreamContent(Stream content, int bufferSize, Action<double, string, string> progress)
        {
            this.content = content;
            this.bufferSize = bufferSize;
            this.progress = progress;
            this.stopwatch = new Stopwatch();
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var buffer = new byte[bufferSize];
            long size = content.Length;
            long uploaded = 0;

            stopwatch.Start();

            using (content)
            {
                int read;
                while ((read = await content.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await stream.WriteAsync(buffer, 0, read);
                    uploaded += read;
                    double progressPercentage = (uploaded / (double)size) * 100;

                    // Calculate speed and remaining time
                    double elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                    double speed = uploaded / elapsedSeconds; // bytes per second
                    double remainingBytes = size - uploaded;
                    double remainingTime = remainingBytes / speed; // seconds

                    string speedFormatted = $"{speed / 1024 / 1024:F2} MB/s";
                    string remainingTimeFormatted = $"{TimeSpan.FromSeconds(remainingTime):hh\\:mm\\:ss}";

                    progress(progressPercentage, speedFormatted, remainingTimeFormatted);
                }
            }

            stopwatch.Stop();
        }

        protected override bool TryComputeLength(out long length)
        {
            length = content.Length;
            return true;
        }
    }

}
