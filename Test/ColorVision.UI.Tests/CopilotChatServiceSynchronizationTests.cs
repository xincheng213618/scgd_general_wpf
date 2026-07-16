#pragma warning disable CA1707
using ColorVision.Copilot;
using System.Net;
using System.Net.Http;
using System.Text;

namespace ColorVision.UI.Tests;

public sealed class CopilotChatServiceSynchronizationTests
{
    [Fact]
    public async Task ChatService_DoesNotMarshalProviderParsingBackToCallerContext()
    {
        using var handler = new GatedResponseHandler();
        using var httpClient = new HttpClient(handler);
        var service = new CopilotChatService(httpClient);
        var callerContext = new ForwardingSynchronizationContext();
        SynchronizationContext? callbackContext = null;
        Task<CopilotTokenUsage> operation;
        var previousContext = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(callerContext);
            operation = service.StreamReplyAsync(
                CreateProfile(),
                [new CopilotRequestMessage("user", "test")],
                _ => callbackContext = SynchronizationContext.Current,
                CancellationToken.None);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }

        handler.Release();
        await operation.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Null(callbackContext);
        Assert.Equal(0, callerContext.PostCount);
    }

    private static CopilotProfileConfig CreateProfile() => new()
    {
        ProviderType = CopilotProviderType.OpenAICompatible,
        ApiKey = "secret-key",
        BaseUrl = "https://example.test/v1",
        Model = "test-model",
        MaxTokens = 256,
    };

    private sealed class GatedResponseHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => _release.TrySetResult();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "data: {\"choices\":[{\"delta\":{\"content\":\"done\"}}]}\n\ndata: [DONE]\n\n",
                    Encoding.UTF8,
                    "text/event-stream"),
            };
        }
    }

    private sealed class ForwardingSynchronizationContext : SynchronizationContext
    {
        private int _postCount;

        public int PostCount => Volatile.Read(ref _postCount);

        public override void Post(SendOrPostCallback d, object? state)
        {
            Interlocked.Increment(ref _postCount);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                var previous = Current;
                SetSynchronizationContext(this);
                try
                {
                    d(state);
                }
                finally
                {
                    SetSynchronizationContext(previous);
                }
            });
        }
    }
}
