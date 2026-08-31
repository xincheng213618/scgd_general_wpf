using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotWebPageDeadlineTests
{
    [Fact]
    public async Task StalledResponseBodyIsCancelledByTheDefaultWebPageDeadline()
    {
        using var callerCancellation = new CancellationTokenSource();
        using var stream = new ControlledResponseStream();
        using var handler = new SingleResponseHandler(HttpStatusCode.OK, stream);
        var operation = LoadAsync(handler, callerCancellation.Token);

        try
        {
            await stream.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            // Exercise the production 20-second deadline, not an injected test timeout.
            // The outer guard only prevents the old implementation from hanging the test.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await operation.WaitAsync(TimeSpan.FromSeconds(27)));

            Assert.False(callerCancellation.IsCancellationRequested);
            Assert.True(stream.LastReadToken.IsCancellationRequested);
            Assert.True(stream.IsDisposed);
            Assert.True(handler.IsDisposed);
            Assert.Equal(1, handler.RequestCount);
        }
        finally
        {
            callerCancellation.Cancel();
            await ObserveCompletionAsync(operation);
        }
    }

    [Fact]
    public async Task CallerCancellationStopsAResponseBodyWithoutWaitingForTheDeadline()
    {
        using var callerCancellation = new CancellationTokenSource();
        using var stream = new ControlledResponseStream();
        using var handler = new SingleResponseHandler(HttpStatusCode.OK, stream);
        var operation = LoadAsync(handler, callerCancellation.Token);

        try
        {
            await stream.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            callerCancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await operation.WaitAsync(TimeSpan.FromSeconds(5)));

            Assert.True(stream.LastReadToken.IsCancellationRequested);
            Assert.True(stream.IsDisposed);
            Assert.True(handler.IsDisposed);
            Assert.Equal(1, handler.RequestCount);
        }
        finally
        {
            callerCancellation.Cancel();
            await ObserveCompletionAsync(operation);
        }
    }

    [Fact]
    public async Task SuccessfulResponseBodyIsReturnedAndItsTransportIsDisposed()
    {
        using var callerCancellation = new CancellationTokenSource();
        using var stream = new ControlledResponseStream(
            "<html><head><title>Deadline control</title></head><body><main>Readable body before the deadline.</main></body></html>");
        using var handler = new SingleResponseHandler(HttpStatusCode.OK, stream);
        var operation = LoadAsync(handler, callerCancellation.Token);

        try
        {
            var page = await operation.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal("https://public.test/page", page.Url);
            Assert.Equal("Deadline control", page.Title);
            Assert.Contains("Readable body before the deadline.", page.Content, StringComparison.Ordinal);
            Assert.False(callerCancellation.IsCancellationRequested);
            Assert.True(stream.ReadStarted.Task.IsCompletedSuccessfully);
            Assert.True(stream.IsDisposed);
            Assert.True(handler.IsDisposed);
            Assert.Equal(1, handler.RequestCount);
        }
        finally
        {
            callerCancellation.Cancel();
            await ObserveCompletionAsync(operation);
        }
    }

    [Fact]
    public async Task HttpErrorIsRejectedWithoutReadingOrSavingItsResponseBody()
    {
        using var callerCancellation = new CancellationTokenSource();
        using var stream = new ControlledResponseStream();
        using var handler = new SingleResponseHandler(HttpStatusCode.NotFound, stream);
        var operation = LoadAsync(handler, callerCancellation.Token);

        try
        {
            var exception = await Assert.ThrowsAsync<HttpRequestException>(async () =>
                await operation.WaitAsync(TimeSpan.FromSeconds(5)));

            Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
            Assert.False(stream.ReadStarted.Task.IsCompleted);
            Assert.False(callerCancellation.IsCancellationRequested);
            Assert.True(stream.IsDisposed);
            Assert.True(handler.IsDisposed);
            Assert.Equal(1, handler.RequestCount);
        }
        finally
        {
            callerCancellation.Cancel();
            await ObserveCompletionAsync(operation);
        }
    }

    private static Task<CopilotFetchedWebPageContent> LoadAsync(
        HttpMessageHandler handler,
        CancellationToken cancellationToken)
    {
        return CopilotWebPageToolSupport.LoadWebPageContentAsync(
            "https://public.test/page",
            static (_, token) =>
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult(new[] { IPAddress.Parse("93.184.216.34") });
            },
            () => handler,
            static () => string.Empty,
            cancellationToken);
    }

    private static async Task ObserveCompletionAsync(Task operation)
    {
        try
        {
            await operation.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception) when (operation.IsCompleted)
        {
            // Observe the original failure without replacing a test assertion in cleanup.
        }
    }

    private sealed class SingleResponseHandler(HttpStatusCode statusCode, Stream stream) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public bool IsDisposed { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestCount++;
            var content = new StreamContent(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/html") { CharSet = "utf-8" };
            return Task.FromResult(new HttpResponseMessage(statusCode) { Content = content });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class ControlledResponseStream(string? content = null) : Stream
    {
        private readonly MemoryStream? _content = content == null ? null : new MemoryStream(Encoding.UTF8.GetBytes(content));
        private readonly TaskCompletionSource<int> _neverCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken LastReadToken { get; private set; }
        public bool IsDisposed { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastReadToken = cancellationToken;
            ReadStarted.TrySetResult();
            return _content != null
                ? await _content.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)
                : await _neverCompleted.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                IsDisposed = true;
                _content?.Dispose();
            }
            base.Dispose(disposing);
        }

        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
