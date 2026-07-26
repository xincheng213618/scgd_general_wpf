using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed class CopilotDelegatedDirectAnswerChatClient : DelegatingChatClient
    {
        private const string DelegateExploreToolName = "DelegateExplore";
        private const int MaximumDirectAnswerCharacters = 8_000;
        private readonly CopilotAgentRequest _request;
        private readonly Func<IReadOnlyList<CopilotAgentStepRecord>> _stepRecordsProvider;
        private readonly Action _onDirectAnswer;
        private readonly bool _taskLedgerEnabled;

        public CopilotDelegatedDirectAnswerChatClient(
            IChatClient innerClient,
            CopilotAgentRequest request,
            Func<IReadOnlyList<CopilotAgentStepRecord>> stepRecordsProvider,
            bool taskLedgerEnabled,
            Action onDirectAnswer)
            : base(innerClient)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _stepRecordsProvider = stepRecordsProvider ?? throw new ArgumentNullException(nameof(stepRecordsProvider));
            _taskLedgerEnabled = taskLedgerEnabled;
            _onDirectAnswer = onDirectAnswer ?? throw new ArgumentNullException(nameof(onDirectAnswer));
        }

        public override Task<ChatResponse> GetResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var materializedMessages = messages?.ToArray() ?? Array.Empty<Microsoft.Extensions.AI.ChatMessage>();
            if (!TryCreateDirectAnswer(materializedMessages, out var answer))
                return base.GetResponseAsync(materializedMessages, options, cancellationToken);

            _onDirectAnswer();
            return Task.FromResult(new ChatResponse(
                new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, answer))
            {
                FinishReason = ChatFinishReason.Stop,
            });
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var materializedMessages = messages?.ToArray() ?? Array.Empty<Microsoft.Extensions.AI.ChatMessage>();
            if (TryCreateDirectAnswer(materializedMessages, out var answer))
            {
                _onDirectAnswer();
                yield return new ChatResponseUpdate(ChatRole.Assistant, answer)
                {
                    FinishReason = ChatFinishReason.Stop,
                };
                yield break;
            }

            await foreach (var update in base.GetStreamingResponseAsync(
                materializedMessages,
                options,
                cancellationToken))
            {
                yield return update;
            }
        }

        private bool TryCreateDirectAnswer(
            Microsoft.Extensions.AI.ChatMessage[] messages,
            out string answer)
        {
            answer = string.Empty;
            if (_taskLedgerEnabled
                || !_request.RequiresDelegatedWorkspaceEvidence
                || _request.RequiredSuccessfulToolNames.Count != 1
                || !string.Equals(
                    _request.RequiredSuccessfulToolNames[0],
                    DelegateExploreToolName,
                    StringComparison.OrdinalIgnoreCase)
                || messages.Length == 0)
            {
                return false;
            }

            var functionResultCallIds = messages[^1].Contents
                .OfType<FunctionResultContent>()
                .Where(result => !string.IsNullOrWhiteSpace(result.CallId))
                .Select(result => result.CallId.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (functionResultCallIds.Length != 1)
                return false;

            var steps = _stepRecordsProvider() ?? Array.Empty<CopilotAgentStepRecord>();
            if (steps.Count != 1)
                return false;

            var step = steps[0];
            return string.Equals(step.ToolCall?.ToolName, DelegateExploreToolName, StringComparison.OrdinalIgnoreCase)
                && step.Observation?.Success == true
                && step.Observation.DelegatedRunUsage?.StopReason == CopilotAgentStopReason.Completed
                && string.Equals(step.Execution?.CallId, functionResultCallIds[0], StringComparison.Ordinal)
                && TryUseCompletedAnswer(step.Observation.DelegatedAnswer, out answer);
        }

        internal static bool TryUseCompletedAnswer(
            CopilotDelegatedAnswer? delegatedAnswer,
            out string answer)
        {
            answer = string.Empty;
            if (delegatedAnswer == null
                || delegatedAnswer.StopReason != CopilotAgentStopReason.Completed
                || !delegatedAnswer.HasSuccessfulEvidence
                || delegatedAnswer.WasTruncated)
            {
                return false;
            }

            var candidate = (delegatedAnswer.Text ?? string.Empty)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Trim();
            if (candidate.Length == 0
                || candidate.Length > MaximumDirectAnswerCharacters
                || !CopilotSubagentRunner.HasCompleteDeclaration(candidate))
            {
                return false;
            }

            var lines = candidate
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length is < 2 or > 6
                || !lines[^1].StartsWith("complete: yes", StringComparison.OrdinalIgnoreCase)
                || lines[..^1].Any(line =>
                    !line.StartsWith("- ", StringComparison.Ordinal)
                    || !line.Contains(" — ", StringComparison.Ordinal)))
            {
                return false;
            }

            answer = candidate;
            return true;
        }
    }
}
