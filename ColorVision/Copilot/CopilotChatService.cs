#pragma warning disable CA1822,CA1861
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal enum CopilotChatFinishKind
    {
        Unspecified,
        Complete,
        LengthLimit,
        ContentFiltered,
        ToolRequested,
        Other,
    }

    internal readonly record struct CopilotChatStreamResult(
        CopilotTokenUsage Usage,
        CopilotChatFinishKind FinishKind,
        string FinishReason)
    {
        public bool IsIncomplete => FinishKind is CopilotChatFinishKind.LengthLimit
            or CopilotChatFinishKind.ContentFiltered
            or CopilotChatFinishKind.ToolRequested
            or CopilotChatFinishKind.Other;
    }

    internal readonly record struct CopilotCompletedReplyResult(
        CopilotChatReply Reply,
        CopilotChatStreamResult StreamResult,
        bool IsContentTruncated)
    {
        public bool IsIncomplete => IsContentTruncated || StreamResult.IsIncomplete;

        public string Content => Reply.Content;

        public CopilotTokenUsage Usage => Reply.Usage;
    }

    internal sealed class CopilotProviderPayloadException : InvalidOperationException
    {
        public CopilotProviderPayloadException(
            string message,
            string errorCode,
            bool isTransient,
            string requestId)
            : base(message)
        {
            ErrorCode = errorCode ?? string.Empty;
            IsTransient = isTransient;
            RequestId = CopilotProviderRequestId.Normalize(requestId);
            CopilotProviderRequestId.Preserve(this, RequestId);
        }

        public string ErrorCode { get; }

        public bool IsTransient { get; }

        public string RequestId { get; }
    }

    public sealed partial class CopilotChatService
    {
        private const int MaximumProviderErrorResponseBytes = 256 * 1024;
        private const int MaximumNonStreamingResponseBytes = 4 * 1024 * 1024;
        private const int MaximumStreamingResponseBytes = 8 * 1024 * 1024;
        private const int MaximumStreamingLineCharacters = 1024 * 1024;
        private const string ProviderStatusCodeDataKey = "ColorVision.Copilot.ProviderStatusCode";
        private static readonly HttpClient SharedHttpClient = CopilotProviderHttpTransport.CreateClient();
        private readonly HttpClient _httpClient;
        private readonly int _maximumAttempts;
        private readonly Func<int, TimeSpan> _retryDelayFactory;
        private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
        private readonly TimeSpan? _firstResponseTimeoutOverride;
        private readonly TimeSpan? _streamingUpdateTimeoutOverride;

        public CopilotChatService()
            : this(SharedHttpClient)
        {
        }

        public CopilotChatService(HttpClient httpClient)
            : this(
                httpClient,
                CopilotProviderRetryChatClient.DefaultMaximumAttempts,
                CopilotProviderRetryChatClient.CreateDefaultDelay,
                Task.Delay)
        {
        }

        internal CopilotChatService(
            HttpClient httpClient,
            int maximumAttempts,
            Func<int, TimeSpan> retryDelayFactory,
            Func<TimeSpan, CancellationToken, Task> delayAsync,
            TimeSpan? firstResponseTimeout = null,
            TimeSpan? streamingUpdateTimeout = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            ArgumentOutOfRangeException.ThrowIfLessThan(maximumAttempts, 1);
            _maximumAttempts = maximumAttempts;
            _retryDelayFactory = retryDelayFactory ?? throw new ArgumentNullException(nameof(retryDelayFactory));
            _delayAsync = delayAsync ?? throw new ArgumentNullException(nameof(delayAsync));
            _firstResponseTimeoutOverride = firstResponseTimeout;
            _streamingUpdateTimeoutOverride = streamingUpdateTimeout;
            if (firstResponseTimeout.HasValue)
            {
                CopilotProviderInactivityPolicy.ValidateTimeout(
                    firstResponseTimeout.Value,
                    nameof(firstResponseTimeout));
            }
            if (streamingUpdateTimeout.HasValue)
            {
                CopilotProviderInactivityPolicy.ValidateTimeout(
                    streamingUpdateTimeout.Value,
                    nameof(streamingUpdateTimeout));
            }
        }



    }
}
