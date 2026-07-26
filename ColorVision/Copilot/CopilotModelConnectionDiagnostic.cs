using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed record CopilotModelConnectionDiagnosticResult(
        TimeSpan Elapsed,
        int DisplayableCharacters,
        int RetryCount,
        CopilotProviderRetryInfo? LatestRetry,
        CopilotChatStreamResult StreamResult)
    {
        public string FormatStatus()
        {
            var builder = new StringBuilder("Connected in ")
                .Append(FormatDuration(Elapsed))
                .Append('.');
            if (DisplayableCharacters > 0)
            {
                builder.Append(" Received ")
                    .Append(DisplayableCharacters.ToString("N0", CultureInfo.InvariantCulture))
                    .Append(" displayable character")
                    .Append(DisplayableCharacters == 1 ? string.Empty : "s")
                    .Append('.');
            }
            else
            {
                builder.Append(" The stream contained no displayable text; verify the model name and streaming compatibility before use.");
            }

            if (RetryCount > 0)
            {
                builder.Append(" Recovered after ")
                    .Append(RetryCount.ToString("N0", CultureInfo.InvariantCulture))
                    .Append(RetryCount == 1 ? " retry" : " retries");
                if (!string.IsNullOrWhiteSpace(LatestRetry?.FailureKind))
                    builder.Append(" (last: ").Append(LatestRetry.FailureKind.Trim()).Append(')');
                builder.Append('.');
            }
            if (StreamResult.IsIncomplete)
            {
                builder.Append(" The diagnostic response ended early: ")
                    .Append(FormatFinishKind(StreamResult.FinishKind))
                    .Append('.');
            }
            return builder.ToString();
        }

        internal static string FormatDuration(TimeSpan duration)
        {
            var milliseconds = Math.Max(0, duration.TotalMilliseconds);
            if (milliseconds < 1000)
                return milliseconds.ToString("0", CultureInfo.InvariantCulture) + " ms";
            if (milliseconds < 60_000)
                return (milliseconds / 1000).ToString("0.#", CultureInfo.InvariantCulture) + " s";
            return (milliseconds / 60_000).ToString("0.#", CultureInfo.InvariantCulture) + " min";
        }

        private static string FormatFinishKind(CopilotChatFinishKind finishKind)
        {
            return finishKind switch
            {
                CopilotChatFinishKind.LengthLimit => "length limit",
                CopilotChatFinishKind.ContentFiltered => "content filtered",
                CopilotChatFinishKind.ToolRequested => "unexpected tool request",
                CopilotChatFinishKind.Other => "provider-specific finish",
                _ => "unspecified finish",
            };
        }
    }

    internal sealed class CopilotModelConnectionDiagnosticException : Exception
    {
        public CopilotModelConnectionDiagnosticException(
            TimeSpan elapsed,
            int retryCount,
            CopilotProviderRetryInfo? latestRetry,
            Exception innerException)
            : base(innerException.Message, innerException)
        {
            Elapsed = elapsed;
            RetryCount = Math.Max(0, retryCount);
            LatestRetry = latestRetry;
        }

        public TimeSpan Elapsed { get; }

        public int RetryCount { get; }

        public CopilotProviderRetryInfo? LatestRetry { get; }
    }

    internal sealed class CopilotModelConnectionDiagnostic
    {
        private const string SystemPrompt = "You are validating a model connection. Reply with OK.";
        private static readonly CopilotRequestMessage[] Messages =
        {
            new("user", "Reply with OK."),
        };

        private readonly CopilotChatService _chatService;

        public CopilotModelConnectionDiagnostic()
            : this(new CopilotChatService())
        {
        }

        internal CopilotModelConnectionDiagnostic(CopilotChatService chatService)
        {
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
        }

        public async Task<CopilotModelConnectionDiagnosticResult> TestAsync(
            CopilotProfileConfig sourceProfile,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(sourceProfile);
            var profile = sourceProfile.Clone();
            profile.EnsureValid();
            if (!profile.IsConfigured)
                throw new InvalidOperationException("The selected model profile is incomplete.");

            profile.UseSystemPromptOverride(SystemPrompt);
            profile.MaxTokens = 128;
            profile.Temperature = 0;

            var displayableCharacters = 0;
            var retries = new List<CopilotProviderRetryInfo>();
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var streamResult = await _chatService.StreamReplyAsync(
                    profile,
                    Messages,
                    delta =>
                    {
                        checked
                        {
                            displayableCharacters += delta.Content.Length;
                            displayableCharacters += delta.ReasoningContent.Length;
                        }
                    },
                    retries.Add,
                    cancellationToken).ConfigureAwait(false);
                return new CopilotModelConnectionDiagnosticResult(
                    stopwatch.Elapsed,
                    displayableCharacters,
                    retries.Count,
                    retries.Count == 0 ? null : retries[^1],
                    streamResult);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new CopilotModelConnectionDiagnosticException(
                    stopwatch.Elapsed,
                    retries.Count,
                    retries.Count == 0 ? null : retries[^1],
                    exception);
            }
        }
    }
}
