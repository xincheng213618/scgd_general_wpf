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
    internal sealed class CopilotAgentTokenBudgetExceededException : Exception
    {
        public CopilotAgentTokenBudgetExceededException()
            : base("This Agent run reached its bounded cumulative token budget; the next provider call was not sent. Reduce context, continue with a new message, or increase the Agent request-token budget.")
        {
        }
    }

    internal sealed class CopilotAgentContextWindowExceededException : Exception
    {
        public CopilotAgentContextWindowExceededException(int estimatedInputTokens, int inputBudgetTokens)
            : base($"This Agent request exceeds its configured context window (estimated input {estimatedInputTokens:N0} tokens; maximum {inputBudgetTokens:N0}). Reduce conversation or attachment context, or increase the Agent context-window setting only when the configured model supports it.")
        {
            EstimatedInputTokens = estimatedInputTokens;
            InputBudgetTokens = inputBudgetTokens;
        }

        public int EstimatedInputTokens { get; }

        public int InputBudgetTokens { get; }
    }

    public sealed class CopilotAgentTokenBudget
    {
        public const int MinimumContextWindowTokens = 32_768;
        public const int MaximumContextWindowTokens = 1_048_576;
        public const int DefaultContextWindowTokens = MaximumContextWindowTokens;

        public int ContextWindowTokens { get; init; }

        public int MaxOutputTokens { get; init; }

        public int InputBudgetTokens => Math.Max(1, ContextWindowTokens - MaxOutputTokens);

        public int RequestTokenBudget { get; init; }

        public static CopilotAgentTokenBudget Create(CopilotProfileConfig profile, CopilotAgentRunBudget runBudget)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(runBudget);
            var maxOutputTokens = Math.Clamp(profile.MaxTokens, 32, CopilotProfileConfig.DefaultMaxTokens);
            return new CopilotAgentTokenBudget
            {
                ContextWindowTokens = Math.Clamp(runBudget.ContextWindowTokens, MinimumContextWindowTokens, MaximumContextWindowTokens),
                MaxOutputTokens = maxOutputTokens,
                RequestTokenBudget = runBudget.RequestTokenBudget,
            };
        }
    }

    internal sealed class CopilotTokenBudgetChatClient : DelegatingChatClient
    {
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

        internal void RecordDelegatedRunUsage(CopilotDelegatedRunUsage delegatedRun)
        {
            ArgumentNullException.ThrowIfNull(delegatedRun);
            lock (_syncRoot)
            {
                _usage = _usage.Add(delegatedRun.Usage);
                _providerCalls += Math.Max(0, delegatedRun.ProviderCalls);
                _consumedTokens += Math.Max(Math.Max(0, delegatedRun.ConsumedTokens), delegatedRun.Usage.EffectiveTotalTokens);
                _usedEstimatedUsage |= delegatedRun.UsedEstimatedUsage;
                if (_consumedTokens >= _budget.RequestTokenBudget)
                    _budgetExhausted = true;
            }
        }

        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var materializedMessages = messages?.ToArray() ?? Array.Empty<Microsoft.Extensions.AI.ChatMessage>();
            var estimatedInputTokens = EstimateInputTokens(materializedMessages, options);
            EnsureWithinContextWindow(estimatedInputTokens);
            if (!TryBeginProviderCall(estimatedInputTokens))
                throw new CopilotAgentTokenBudgetExceededException();

            ChatResponse response;
            try
            {
                response = await base.GetResponseAsync(materializedMessages, options, cancellationToken);
            }
            catch
            {
                CommitUsage(CopilotTokenUsage.Empty, estimatedInputTokens, requireEstimatedFloor: true);
                throw;
            }
            var usage = ExtractUsage(response.Messages.SelectMany(message => message.Contents));
            CommitUsage(usage, EstimateTokens(materializedMessages, options, EstimateMessageWeight(response.Messages)));
            return response;
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var materializedMessages = messages?.ToArray() ?? Array.Empty<Microsoft.Extensions.AI.ChatMessage>();
            var estimatedInputTokens = EstimateInputTokens(materializedMessages, options);
            EnsureWithinContextWindow(estimatedInputTokens);
            if (!TryBeginProviderCall(estimatedInputTokens))
                throw new CopilotAgentTokenBudgetExceededException();

            var usage = CopilotTokenUsage.Empty;
            long responseWeight = 0;
            var completed = false;
            try
            {
                await foreach (var update in base.GetStreamingResponseAsync(materializedMessages, options, cancellationToken))
                {
                    responseWeight += EstimateContentWeight(update.Contents);
                    usage = usage.MergeProgress(ExtractUsage(update.Contents));
                    yield return update;
                }
                completed = true;
            }
            finally
            {
                CommitUsage(
                    usage,
                    EstimateTokens(materializedMessages, options, responseWeight),
                    requireEstimatedFloor: !completed);
            }
        }

        private void EnsureWithinContextWindow(int estimatedInputTokens)
        {
            if (estimatedInputTokens <= _budget.InputBudgetTokens)
                return;

            lock (_syncRoot)
            {
                _budgetExhausted = true;
                _usedEstimatedUsage = true;
            }
            throw new CopilotAgentContextWindowExceededException(estimatedInputTokens, _budget.InputBudgetTokens);
        }

        private bool TryBeginProviderCall(int estimatedInputTokens)
        {
            CopilotAgentBudgetSnapshot? notification = null;
            lock (_syncRoot)
            {
                var wouldExceedBudget = _consumedTokens + Math.Max(1, estimatedInputTokens) > _budget.RequestTokenBudget;
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

        private void CommitUsage(CopilotTokenUsage actualUsage, int estimatedTokens, bool requireEstimatedFloor = false)
        {
            lock (_syncRoot)
            {
                var consumedTokens = Math.Max(1, estimatedTokens);
                if (actualUsage.HasAny)
                {
                    _usage = _usage.Add(actualUsage);
                    var actualTokens = Math.Max(1, actualUsage.EffectiveTotalTokens);
                    if (requireEstimatedFloor && actualTokens < consumedTokens)
                        _usedEstimatedUsage = true;
                    else
                        consumedTokens = actualTokens;
                }
                else
                {
                    _usedEstimatedUsage = true;
                }
                _consumedTokens += consumedTokens;

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
            long responseWeight)
        {
            var weight = EstimateInputWeight(messages, options) + Math.Max(0, responseWeight);
            return WeightToTokenEstimate(weight);
        }

        private static int EstimateInputTokens(
            IReadOnlyList<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options)
        {
            return WeightToTokenEstimate(EstimateInputWeight(messages, options));
        }

        private static long EstimateInputWeight(
            IReadOnlyList<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options)
        {
            var weight = EstimateTextWeight(options?.Instructions);
            weight += EstimateToolsWeight(options?.Tools);
            weight += EstimateMessageWeight(messages);
            return weight;
        }

        private static long EstimateMessageWeight(IEnumerable<Microsoft.Extensions.AI.ChatMessage>? messages)
        {
            long weight = 0;
            foreach (var message in messages ?? Enumerable.Empty<Microsoft.Extensions.AI.ChatMessage>())
            {
                weight += 16;
                weight += EstimateContentWeight(message.Contents);
            }
            return weight;
        }

        private static long EstimateContentWeight(IEnumerable<AIContent>? contents)
        {
            long weight = 0;
            foreach (var content in contents ?? Enumerable.Empty<AIContent>())
                weight += EstimateContentWeight(content);
            return weight;
        }

        private static long EstimateContentWeight(AIContent? content)
        {
            return content switch
            {
                null => 0,
                TextContent text => EstimateTextWeight(text.Text),
                TextReasoningContent reasoning => EstimateTextWeight(reasoning.Text) + EstimateTextWeight(reasoning.ProtectedData),
                FunctionCallContent functionCall => EstimateTextWeight(functionCall.CallId)
                    + EstimateTextWeight(functionCall.Name)
                    + EstimateValueWeight(functionCall.Arguments)
                    + EstimateTextWeight(functionCall.Exception?.Message),
                FunctionResultContent functionResult => EstimateTextWeight(functionResult.CallId)
                    + EstimateValueWeight(functionResult.Result)
                    + EstimateTextWeight(functionResult.Exception?.Message),
                ToolApprovalRequestContent approvalRequest => EstimateTextWeight(approvalRequest.RequestId)
                    + EstimateContentWeight(approvalRequest.ToolCall),
                ToolApprovalResponseContent approvalResponse => EstimateTextWeight(approvalResponse.RequestId)
                    + EstimateTextWeight(approvalResponse.Reason)
                    + EstimateContentWeight(approvalResponse.ToolCall),
                ErrorContent error => EstimateTextWeight(error.Message)
                    + EstimateTextWeight(error.ErrorCode)
                    + EstimateTextWeight(error.Details),
                DataContent data => EstimateDataContentWeight(data),
                UriContent uri => EstimateTextWeight(uri.Uri?.OriginalString) + EstimateTextWeight(uri.MediaType),
                _ => EstimateTextWeight(content.ToString()),
            };
        }

        private static long EstimateToolsWeight(IEnumerable<AITool>? tools)
        {
            long weight = 0;
            foreach (var tool in tools ?? Enumerable.Empty<AITool>())
            {
                weight += EstimateTextWeight(tool.Name);
                weight += EstimateTextWeight(tool.Description);
                if (tool is AIFunction function)
                {
                    weight += function.JsonSchema.ValueKind == JsonValueKind.Undefined
                        ? 0
                        : EstimateTextWeight(function.JsonSchema.GetRawText());
                    if (function.ReturnJsonSchema is JsonElement returnSchema
                        && returnSchema.ValueKind != JsonValueKind.Undefined)
                    {
                        weight += EstimateTextWeight(returnSchema.GetRawText());
                    }
                }
            }
            return weight;
        }

        private static long EstimateValueWeight(object? value)
        {
            if (value == null)
                return 4;
            if (value is string text)
                return EstimateTextWeight(text);
            if (value is JsonElement element)
                return EstimateTextWeight(element.GetRawText());

            try
            {
                return EstimateTextWeight(JsonSerializer.Serialize(value, value.GetType()));
            }
            catch (Exception ex) when (ex is NotSupportedException or JsonException)
            {
                return EstimateTextWeight(value.ToString());
            }
        }

        private static long EstimateDataContentWeight(DataContent data)
        {
            var encodedWeight = EstimateEncodedDataWeight(data.Data.Length);
            if (encodedWeight == 0)
                encodedWeight = EstimateTextWeight(data.Uri);
            return encodedWeight + EstimateTextWeight(data.MediaType) + EstimateTextWeight(data.Name);
        }

        private static long EstimateEncodedDataWeight(int byteCount)
        {
            return byteCount <= 0 ? 0 : ((long)byteCount + 2) / 3 * 4;
        }

        private static long EstimateTextWeight(string? value)
        {
            // Approximate ASCII-heavy prompts at four characters per token while treating
            // CJK and other non-ASCII text as roughly one UTF-16 code unit per token.
            long weight = 0;
            foreach (var character in value ?? string.Empty)
                weight += character <= 0x7f ? 1 : 4;
            return weight;
        }

        private static int WeightToTokenEstimate(long weight)
        {
            var normalized = Math.Max(1, weight);
            var tokens = normalized / 4 + (normalized % 4 == 0 ? 0 : 1);
            return (int)Math.Clamp(tokens, 1, int.MaxValue);
        }
    }
}
