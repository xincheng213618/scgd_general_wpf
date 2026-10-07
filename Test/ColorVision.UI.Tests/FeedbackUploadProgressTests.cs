using ColorVision.UI.Desktop.Feedback;
using System.IO;
using System.Net;
using System.Net.Http;

namespace ColorVision.UI.Tests;

public sealed class FeedbackUploadProgressTests
{
    [Fact]
    public async Task UploadContentStreamsBytesAndReportsIntermediateProgress()
    {
        using var source = new StreamingOnlyContent();
        List<double> updates = [];
        using var content = new ProgressableStreamContent(source, new InlineProgress(updates.Add));

        await content.CopyToAsync(Stream.Null);

        Assert.Equal(4, source.ChunksWritten);
        Assert.Contains(updates, value => value > 0 && value < 100);
        Assert.Equal(100, updates[^1]);
    }

    private sealed class StreamingOnlyContent : HttpContent
    {
        public int ChunksWritten { get; private set; }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            byte[] chunk = new byte[4096];
            for (int index = 0; index < 4; index++)
            {
                await stream.WriteAsync(chunk);
                ChunksWritten++;
            }
        }

        protected override Task<Stream> CreateContentReadStreamAsync()
            => throw new InvalidOperationException("Upload must not buffer the source content.");

        protected override bool TryComputeLength(out long length)
        {
            length = 4 * 4096;
            return true;
        }
    }

    private sealed class InlineProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
