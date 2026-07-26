using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed class CopilotExplicitDelegationDispatchChatClient : DelegatingChatClient
    {
        private const string DelegateExploreToolName = "DelegateExplore";
        private readonly string _delegateFunctionName;
        private readonly string _task;
        private readonly Action _onDispatch;
        private readonly bool _enabled;
        private int _dispatchState;

        public CopilotExplicitDelegationDispatchChatClient(
            IChatClient innerClient,
            CopilotAgentRequest request,
            string delegateFunctionName,
            bool taskLedgerEnabled,
            Action onDispatch)
            : base(innerClient)
        {
            ArgumentNullException.ThrowIfNull(request);
            _delegateFunctionName = (delegateFunctionName ?? string.Empty).Trim();
            _onDispatch = onDispatch ?? throw new ArgumentNullException(nameof(onDispatch));
            _enabled = TryResolveTask(request, taskLedgerEnabled, out _task)
                && _delegateFunctionName.Length > 0;
        }

        public override Task<ChatResponse> GetResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var materializedMessages = messages?.ToArray() ?? Array.Empty<Microsoft.Extensions.AI.ChatMessage>();
            if (!TryCreateToolCall(materializedMessages, options, out var toolCall))
                return base.GetResponseAsync(materializedMessages, options, cancellationToken);

            _onDispatch();
            return Task.FromResult(new ChatResponse(
                new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, [toolCall]))
            {
                FinishReason = ChatFinishReason.ToolCalls,
            });
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var materializedMessages = messages?.ToArray() ?? Array.Empty<Microsoft.Extensions.AI.ChatMessage>();
            if (TryCreateToolCall(materializedMessages, options, out var toolCall))
            {
                _onDispatch();
                yield return new ChatResponseUpdate(ChatRole.Assistant, [toolCall])
                {
                    FinishReason = ChatFinishReason.ToolCalls,
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

        private bool TryCreateToolCall(
            Microsoft.Extensions.AI.ChatMessage[] messages,
            ChatOptions? options,
            out FunctionCallContent toolCall)
        {
            toolCall = null!;
            if (!_enabled
                || messages.Length == 0
                || !messages.Any(message => message.Role == ChatRole.User)
                || messages.SelectMany(message => message.Contents).Any(content =>
                    content is FunctionCallContent or FunctionResultContent)
                || !(options?.Tools ?? Array.Empty<AITool>()).Any(tool =>
                    string.Equals(tool?.Name, _delegateFunctionName, StringComparison.OrdinalIgnoreCase))
                || Interlocked.CompareExchange(ref _dispatchState, 1, 0) != 0)
            {
                return false;
            }

            toolCall = new FunctionCallContent(
                $"call-explicit-delegate-{Guid.NewGuid():N}",
                _delegateFunctionName,
                new Dictionary<string, object?>
                {
                    ["task"] = _task,
                });
            return true;
        }

        private static bool TryResolveTask(
            CopilotAgentRequest request,
            bool taskLedgerEnabled,
            out string task)
        {
            task = (request.UserText ?? string.Empty).Trim();
            var taskIntent = (request.TaskIntentText ?? string.Empty).Trim();
            return !taskLedgerEnabled
                && request.RequiresDelegatedWorkspaceEvidence
                && CopilotToolIntentPolicy.ExplicitlyRequiresDelegatedWorkspaceEvidence(request)
                && request.RequiredSuccessfulToolNames.Count == 1
                && string.Equals(
                    request.RequiredSuccessfulToolNames[0],
                    DelegateExploreToolName,
                    StringComparison.OrdinalIgnoreCase)
                && request.SessionCheckpoint == null
                && request.Recovery == null
                && request.History.Count == 0
                && request.Attachments.Count == 0
                && request.SearchRootPaths.Count > 0
                && task.Length is > 0 and <= CopilotSubagentRunner.MaximumTaskCharacters
                && !task.Contains('\0')
                && (taskIntent.Length == 0 || string.Equals(taskIntent, task, StringComparison.Ordinal));
        }
    }
}
