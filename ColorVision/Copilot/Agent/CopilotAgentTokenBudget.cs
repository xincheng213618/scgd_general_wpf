using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotAgentTokenBudget
    {
        public const int DefaultContextWindowTokens = 32768;
        public const int DefaultRequestWindowMultiplier = 2;

        public int ContextWindowTokens { get; init; }

        public int MaxOutputTokens { get; init; }

        public int InputBudgetTokens => Math.Max(1, ContextWindowTokens - MaxOutputTokens);

        public int RequestTokenBudget { get; init; }

        public static CopilotAgentTokenBudget Create(CopilotProfileConfig profile, CopilotAgentRunBudget runBudget)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(runBudget);
            var maxOutputTokens = Math.Clamp(profile.MaxTokens, 32, CopilotProfileConfig.DefaultMaxTokens);
            var contextWindowTokens = Math.Max(DefaultContextWindowTokens, maxOutputTokens * 4);
            return new CopilotAgentTokenBudget
            {
                ContextWindowTokens = contextWindowTokens,
                MaxOutputTokens = maxOutputTokens,
                RequestTokenBudget = runBudget.RequestTokenBudget,
            };
        }
    }

    internal sealed class CopilotTokenBudgetChatClient : DelegatingChatClient
    {
        private const string BudgetExhaustedAnswer = "This Agent run reached its bounded token budget after completing the recorded tool calls. Continue in a new message if additional work is needed.";

        private readonly CopilotAgentTokenBudget _budget;
        private readonly Action<CopilotAgentBudgetSnapshot>? _onBudgetExhausted;
        private readonly object _syncRoot = new();
        private CopilotTokenUsage _usage;
        private int _providerCalls;
        private long _consumedTokens;
        private bool _usedEstimatedUsage;
        private bool _budgetExhausted;
        private bool _budgetNotificationPublished;

        public CopilotTokenBudgetChatClient(
            IChatClient innerClient,
            CopilotAgentTokenBudget budget,
            Action<CopilotAgentBudgetSnapshot>? onBudgetExhausted = null)
            : base(innerClient)
        {
            _budget = budget ?? throw new ArgumentNullException(nameof(budget));
            _onBudgetExhausted = onBudgetExhausted;
        }

        public CopilotAgentBudgetSnapshot Snapshot
        {
            get
            {
                lock (_syncRoot)
                    return CreateSnapshot();
            }
        }

        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var materializedMessages = messages?.ToArray() ?? Array.Empty<Microsoft.Extensions.AI.ChatMessage>();
            if (!TryBeginProviderCall(EstimateInputTokens(materializedMessages, options)))
                return new ChatResponse(new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, BudgetExhaustedAnswer));

            var response = await base.GetResponseAsync(materializedMessages, options, cancellationToken);
            var usage = ExtractUsage(response.Messages.SelectMany(message => message.Contents));
            CommitUsage(usage, EstimateTokens(materializedMessages, options, EstimateMessageCharacters(response.Messages)));
            return response;
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var materializedMessages = messages?.ToArray() ?? Array.Empty<Microsoft.Extensions.AI.ChatMessage>();
            if (!TryBeginProviderCall(EstimateInputTokens(materializedMessages, options)))
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, BudgetExhaustedAnswer);
                yield break;
            }

            var usage = CopilotTokenUsage.Empty;
            long responseCharacters = 0;
            await foreach (var update in base.GetStreamingResponseAsync(materializedMessages, options, cancellationToken))
            {
                responseCharacters += EstimateContentCharacters(update.Contents);
                usage = usage.MergeProgress(ExtractUsage(update.Contents));
                yield return update;
            }

            CommitUsage(usage, EstimateTokens(materializedMessages, options, responseCharacters));
        }

        private bool TryBeginProviderCall(int estimatedInputTokens)
        {
            CopilotAgentBudgetSnapshot? notification = null;
            lock (_syncRoot)
            {
                var wouldExceedBudget = _providerCalls > 0
                    && _consumedTokens + Math.Max(1, estimatedInputTokens) > _budget.RequestTokenBudget;
                if (_consumedTokens >= _budget.RequestTokenBudget || wouldExceedBudget)
                {
                    _budgetExhausted = true;
                    if (!_budgetNotificationPublished)
                    {
                        _budgetNotificationPublished = true;
                        notification = CreateSnapshot();
                    }
                }
                else
                {
                    _providerCalls++;
                    return true;
                }
            }

            if (notification != null)
                _onBudgetExhausted?.Invoke(notification);
            return false;
        }

        private void CommitUsage(CopilotTokenUsage actualUsage, int estimatedTokens)
        {
            lock (_syncRoot)
            {
                if (actualUsage.HasAny)
                {
                    _usage = _usage.Add(actualUsage);
                    _consumedTokens += Math.Max(1, actualUsage.EffectiveTotalTokens);
                }
                else
                {
                    _usedEstimatedUsage = true;
                    _consumedTokens += Math.Max(1, estimatedTokens);
                }

                if (_consumedTokens >= _budget.RequestTokenBudget)
                    _budgetExhausted = true;
            }
        }

        private CopilotAgentBudgetSnapshot CreateSnapshot()
        {
            return new CopilotAgentBudgetSnapshot
            {
                CompactionEnabled = true,
                ContextWindowTokens = _budget.ContextWindowTokens,
                InputBudgetTokens = _budget.InputBudgetTokens,
                RequestTokenBudget = _budget.RequestTokenBudget,
                ConsumedTokens = Math.Max(0, _consumedTokens),
                ProviderCalls = Math.Max(0, _providerCalls),
                UsedEstimatedUsage = _usedEstimatedUsage,
                BudgetExhausted = _budgetExhausted,
            };
        }

        private static CopilotTokenUsage ExtractUsage(IEnumerable<AIContent>? contents)
        {
            var usage = CopilotTokenUsage.Empty;
            foreach (var usageContent in contents?.OfType<UsageContent>() ?? Enumerable.Empty<UsageContent>())
            {
                var details = usageContent.Details;
                static int ToInt(long? value) => value.HasValue ? (int)Math.Clamp(value.Value, 0, int.MaxValue) : 0;
                usage = usage.MergeProgress(new CopilotTokenUsage(
                    ToInt(details.InputTokenCount),
                    ToInt(details.OutputTokenCount),
                    ToInt(details.TotalTokenCount)));
            }

            return usage;
        }

        private static int EstimateTokens(
            IReadOnlyList<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options,
            long responseCharacters)
        {
            long characters = EstimateInputCharacters(messages, options);
            characters += Math.Max(0, responseCharacters);
            return (int)Math.Clamp((characters + 3) / 4, 1, int.MaxValue);
        }

        private static int EstimateInputTokens(
            IReadOnlyList<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options)
        {
            var characters = EstimateInputCharacters(messages, options);
            return (int)Math.Clamp((characters + 3) / 4, 1, int.MaxValue);
        }

        private static long EstimateInputCharacters(
            IReadOnlyList<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options)
        {
            long characters = options?.Instructions?.Length ?? 0;
            characters += EstimateToolsCharacters(options?.Tools);
            characters += EstimateMessageCharacters(messages);
            return characters;
        }

        private static long EstimateMessageCharacters(IEnumerable<Microsoft.Extensions.AI.ChatMessage>? messages)
        {
            long characters = 0;
            foreach (var message in messages ?? Enumerable.Empty<Microsoft.Extensions.AI.ChatMessage>())
            {
                characters += 16;
                characters += EstimateContentCharacters(message.Contents);
            }
            return characters;
        }

        private static int EstimateContentCharacters(IEnumerable<AIContent>? contents)
        {
            long characters = 0;
            foreach (var content in contents ?? Enumerable.Empty<AIContent>())
                characters += EstimateContentCharacters(content);
            return (int)Math.Clamp(characters, 0, int.MaxValue);
        }

        private static int EstimateContentCharacters(AIContent? content)
        {
            long characters = content switch
            {
                null => 0,
                TextContent text => text.Text?.Length ?? 0,
                TextReasoningContent reasoning => (reasoning.Text?.Length ?? 0) + (reasoning.ProtectedData?.Length ?? 0),
                FunctionCallContent functionCall => (functionCall.CallId?.Length ?? 0)
                    + (functionCall.Name?.Length ?? 0)
                    + EstimateValueCharacters(functionCall.Arguments)
                    + (functionCall.Exception?.Message?.Length ?? 0),
                FunctionResultContent functionResult => (functionResult.CallId?.Length ?? 0)
                    + EstimateValueCharacters(functionResult.Result)
                    + (functionResult.Exception?.Message?.Length ?? 0),
                ToolApprovalRequestContent approvalRequest => (approvalRequest.RequestId?.Length ?? 0)
                    + EstimateContentCharacters(approvalRequest.ToolCall),
                ToolApprovalResponseContent approvalResponse => (approvalResponse.RequestId?.Length ?? 0)
                    + (approvalResponse.Reason?.Length ?? 0)
                    + EstimateContentCharacters(approvalResponse.ToolCall),
                ErrorContent error => (error.Message?.Length ?? 0)
                    + (error.ErrorCode?.Length ?? 0)
                    + (error.Details?.Length ?? 0),
                DataContent data => EstimateDataContentCharacters(data),
                UriContent uri => (uri.Uri?.OriginalString.Length ?? 0) + (uri.MediaType?.Length ?? 0),
                _ => content.ToString()?.Length ?? 0,
            };
            return (int)Math.Clamp(characters, 0, int.MaxValue);
        }

        private static long EstimateToolsCharacters(IEnumerable<AITool>? tools)
        {
            long characters = 0;
            foreach (var tool in tools ?? Enumerable.Empty<AITool>())
            {
                characters += tool.Name?.Length ?? 0;
                characters += tool.Description?.Length ?? 0;
                if (tool is AIFunction function)
                {
                    characters += function.JsonSchema.ValueKind == JsonValueKind.Undefined
                        ? 0
                        : function.JsonSchema.GetRawText().Length;
                    if (function.ReturnJsonSchema is JsonElement returnSchema
                        && returnSchema.ValueKind != JsonValueKind.Undefined)
                    {
                        characters += returnSchema.GetRawText().Length;
                    }
                }
            }
            return characters;
        }

        private static int EstimateValueCharacters(object? value)
        {
            if (value == null)
                return 4;
            if (value is string text)
                return text.Length;
            if (value is JsonElement element)
                return element.GetRawText().Length;

            try
            {
                return JsonSerializer.Serialize(value, value.GetType()).Length;
            }
            catch (Exception ex) when (ex is NotSupportedException or JsonException)
            {
                return value.ToString()?.Length ?? 0;
            }
        }

        private static long EstimateDataContentCharacters(DataContent data)
        {
            var encodedCharacters = EstimateEncodedDataCharacters(data.Data.Length);
            if (encodedCharacters == 0)
                encodedCharacters = data.Uri?.Length ?? 0;
            return encodedCharacters + (data.MediaType?.Length ?? 0) + (data.Name?.Length ?? 0);
        }

        private static long EstimateEncodedDataCharacters(int byteCount)
        {
            return byteCount <= 0 ? 0 : ((long)byteCount + 2) / 3 * 4;
        }
    }
}
