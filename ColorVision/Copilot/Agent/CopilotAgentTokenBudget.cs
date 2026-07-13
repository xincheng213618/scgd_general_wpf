using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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

        public static CopilotAgentTokenBudget Create(CopilotProfileConfig profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            var maxOutputTokens = Math.Clamp(profile.MaxTokens, 32, CopilotProfileConfig.DefaultMaxTokens);
            var contextWindowTokens = Math.Max(DefaultContextWindowTokens, maxOutputTokens * 4);
            return new CopilotAgentTokenBudget
            {
                ContextWindowTokens = contextWindowTokens,
                MaxOutputTokens = maxOutputTokens,
                RequestTokenBudget = checked(contextWindowTokens * DefaultRequestWindowMultiplier),
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
            if (!TryBeginProviderCall())
                return new ChatResponse(new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, BudgetExhaustedAnswer));

            var response = await base.GetResponseAsync(materializedMessages, options, cancellationToken);
            var usage = ExtractUsage(response.Messages.SelectMany(message => message.Contents));
            CommitUsage(usage, EstimateTokens(materializedMessages, options, response.Text?.Length ?? 0));
            return response;
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var materializedMessages = messages?.ToArray() ?? Array.Empty<Microsoft.Extensions.AI.ChatMessage>();
            if (!TryBeginProviderCall())
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, BudgetExhaustedAnswer);
                yield break;
            }

            var usage = CopilotTokenUsage.Empty;
            var responseCharacters = 0;
            await foreach (var update in base.GetStreamingResponseAsync(materializedMessages, options, cancellationToken))
            {
                responseCharacters += update.Text?.Length ?? 0;
                usage = usage.MergeProgress(ExtractUsage(update.Contents));
                yield return update;
            }

            CommitUsage(usage, EstimateTokens(materializedMessages, options, responseCharacters));
        }

        private bool TryBeginProviderCall()
        {
            CopilotAgentBudgetSnapshot? notification = null;
            lock (_syncRoot)
            {
                if (_consumedTokens >= _budget.RequestTokenBudget)
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
            int responseCharacters)
        {
            long characters = options?.Instructions?.Length ?? 0;
            foreach (var message in messages)
                characters += message.Text?.Length ?? 0;
            characters += Math.Max(0, responseCharacters);
            return (int)Math.Clamp((characters + 3) / 4, 1, int.MaxValue);
        }
    }
}
