using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    internal sealed partial class CopilotTokenBudgetChatClient : DelegatingChatClient
    {
        private readonly CopilotAgentTokenBudget _budget;
        private readonly Action<CopilotAgentBudgetSnapshot>? _onBudgetExhausted;
        private readonly Action<CopilotAgentBudgetSnapshot>? _onBudgetChanged;
        private readonly object _syncRoot = new();
        private CopilotTokenUsage _usage;
        private int _providerCalls;
        private int _peakEstimatedInputTokens;
        private int _providerRetryCount;
        private int _providerRateLimitRetryCount;
        private long _providerRetryDelayMs;
        private int _providerFirstContentTimeoutCount;
        private int _providerStreamInactivityTimeoutCount;
        private int _providerResponseCount;
        private long _providerFirstResponseLatencyTotalMs;
        private long _providerFirstResponseLatencyMaxMs;
        private long _providerCallDurationTotalMs;
        private int _providerStreamChunkCount;
        private int _providerStreamInterChunkLatencyCount;
        private long _providerStreamInterChunkLatencyTotalMs;
        private long _providerStreamInterChunkLatencyMaxMs;
        private int _contextRecoveryCount;
        private long _contextRecoveryEstimatedInputTokensBefore;
        private long _contextRecoveryEstimatedInputTokensAfter;
        private long _consumedTokens;
        private bool _usedEstimatedUsage;
        private bool _budgetExhausted;
        private bool _budgetNotificationPublished;

        public CopilotTokenBudgetChatClient(
            IChatClient innerClient,
            CopilotAgentTokenBudget budget,
            Action<CopilotAgentBudgetSnapshot>? onBudgetExhausted = null,
            Action<CopilotAgentBudgetSnapshot>? onBudgetChanged = null)
            : base(innerClient)
        {
            _budget = budget ?? throw new ArgumentNullException(nameof(budget));
            _onBudgetExhausted = onBudgetExhausted;
            _onBudgetChanged = onBudgetChanged;
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
            CopilotAgentBudgetSnapshot snapshot;
            lock (_syncRoot)
            {
                var delegatedProviderCalls = Math.Max(0, delegatedRun.ProviderCalls);
                var delegatedProviderRetryCount = Math.Clamp(
                    delegatedRun.ProviderRetryCount,
                    0,
                    delegatedProviderCalls);
                _usage = _usage.Add(delegatedRun.Usage);
                _providerCalls = AddClamped(_providerCalls, delegatedProviderCalls);
                _peakEstimatedInputTokens = Math.Max(
                    _peakEstimatedInputTokens,
                    Math.Max(0, delegatedRun.PeakEstimatedInputTokens));
                _providerRetryCount = AddClamped(
                    _providerRetryCount,
                    delegatedProviderRetryCount);
                _providerRateLimitRetryCount = Math.Min(
                    _providerRetryCount,
                    AddClamped(
                        _providerRateLimitRetryCount,
                        Math.Clamp(
                            delegatedRun.ProviderRateLimitRetryCount,
                            0,
                            delegatedProviderRetryCount)));
                if (delegatedProviderRetryCount > 0)
                {
                    _providerRetryDelayMs = AddClamped(
                        _providerRetryDelayMs,
                        delegatedRun.ProviderRetryDelayMs);
                }
                var delegatedFirstContentTimeoutCount = Math.Clamp(
                    delegatedRun.ProviderFirstContentTimeoutCount,
                    0,
                    delegatedProviderCalls);
                var delegatedStreamInactivityTimeoutCount = Math.Clamp(
                    delegatedRun.ProviderStreamInactivityTimeoutCount,
                    0,
                    delegatedProviderCalls - delegatedFirstContentTimeoutCount);
                _providerFirstContentTimeoutCount = Math.Min(
                    _providerCalls,
                    AddClamped(
                        _providerFirstContentTimeoutCount,
                        delegatedFirstContentTimeoutCount));
                _providerStreamInactivityTimeoutCount = Math.Min(
                    Math.Max(0, _providerCalls - _providerFirstContentTimeoutCount),
                    AddClamped(
                        _providerStreamInactivityTimeoutCount,
                        delegatedStreamInactivityTimeoutCount));
                var delegatedProviderResponseCount = Math.Clamp(
                    delegatedRun.ProviderResponseCount,
                    0,
                    delegatedProviderCalls);
                var delegatedFirstResponseLatencyTotalMs = delegatedProviderResponseCount > 0
                    ? Math.Max(0, delegatedRun.ProviderFirstResponseLatencyTotalMs)
                    : 0;
                _providerResponseCount = Math.Min(
                    _providerCalls,
                    AddClamped(_providerResponseCount, delegatedProviderResponseCount));
                _providerFirstResponseLatencyTotalMs = AddClamped(
                    _providerFirstResponseLatencyTotalMs,
                    delegatedFirstResponseLatencyTotalMs);
                _providerFirstResponseLatencyMaxMs = Math.Max(
                    _providerFirstResponseLatencyMaxMs,
                    Math.Clamp(
                        delegatedRun.ProviderFirstResponseLatencyMaxMs,
                        0,
                        delegatedFirstResponseLatencyTotalMs));
                _providerCallDurationTotalMs = AddClamped(
                    _providerCallDurationTotalMs,
                    Math.Max(
                        delegatedFirstResponseLatencyTotalMs,
                        delegatedRun.ProviderCallDurationTotalMs));
                var delegatedStreamChunkCount = delegatedProviderResponseCount > 0
                    ? Math.Max(0, delegatedRun.ProviderStreamChunkCount)
                    : 0;
                var delegatedStreamInterChunkLatencyCount = Math.Clamp(
                    delegatedRun.ProviderStreamInterChunkLatencyCount,
                    0,
                    Math.Max(0, delegatedStreamChunkCount - 1));
                var delegatedStreamInterChunkLatencyTotalMs = delegatedStreamInterChunkLatencyCount > 0
                    ? Math.Max(0, delegatedRun.ProviderStreamInterChunkLatencyTotalMs)
                    : 0;
                _providerStreamChunkCount = AddClamped(
                    _providerStreamChunkCount,
                    delegatedStreamChunkCount);
                _providerStreamInterChunkLatencyCount = AddClamped(
                    _providerStreamInterChunkLatencyCount,
                    delegatedStreamInterChunkLatencyCount);
                _providerStreamInterChunkLatencyTotalMs = AddClamped(
                    _providerStreamInterChunkLatencyTotalMs,
                    delegatedStreamInterChunkLatencyTotalMs);
                _providerStreamInterChunkLatencyMaxMs = Math.Max(
                    _providerStreamInterChunkLatencyMaxMs,
                    Math.Clamp(
                        delegatedRun.ProviderStreamInterChunkLatencyMaxMs,
                        0,
                        delegatedStreamInterChunkLatencyTotalMs));
                _contextRecoveryCount = AddClamped(
                    _contextRecoveryCount,
                    delegatedRun.ContextRecoveryCount);
                _contextRecoveryEstimatedInputTokensBefore = AddClamped(
                    _contextRecoveryEstimatedInputTokensBefore,
                    delegatedRun.ContextRecoveryEstimatedInputTokensBefore);
                _contextRecoveryEstimatedInputTokensAfter = Math.Min(
                    _contextRecoveryEstimatedInputTokensBefore,
                    AddClamped(
                        _contextRecoveryEstimatedInputTokensAfter,
                        delegatedRun.ContextRecoveryEstimatedInputTokensAfter));
                _consumedTokens = AddClamped(
                    _consumedTokens,
                    Math.Max(Math.Max(0, delegatedRun.ConsumedTokens), delegatedRun.Usage.EffectiveTotalTokens));
                _usedEstimatedUsage |= delegatedRun.UsedEstimatedUsage;
                if (_consumedTokens >= _budget.RequestTokenBudget)
                    _budgetExhausted = true;
                snapshot = CreateSnapshot();
            }
            PublishBudgetChanged(snapshot);
        }

        internal void RecordProviderRetry(CopilotProviderRetryInfo retry)
        {
            ArgumentNullException.ThrowIfNull(retry);
            CopilotAgentBudgetSnapshot snapshot;
            lock (_syncRoot)
            {
                _providerRetryCount = AddClamped(_providerRetryCount, 1);
                if (retry.StatusCode == 429)
                    _providerRateLimitRetryCount = AddClamped(_providerRateLimitRetryCount, 1);
                _providerRetryDelayMs = AddClamped(
                    _providerRetryDelayMs,
                    ToMilliseconds(retry.Delay));
                snapshot = CreateSnapshot();
            }
            PublishBudgetChanged(snapshot);
        }

        private void RecordProviderInactivity(Exception exception)
        {
            if (!CopilotProviderInactivityException.TryFind(
                exception,
                out var inactivity))
            {
                return;
            }

            lock (_syncRoot)
            {
                if (inactivity.Phase == CopilotProviderInactivityPhase.FirstResponse)
                {
                    _providerFirstContentTimeoutCount = AddClamped(
                        _providerFirstContentTimeoutCount,
                        1);
                }
                else
                {
                    _providerStreamInactivityTimeoutCount = AddClamped(
                        _providerStreamInactivityTimeoutCount,
                        1);
                }
            }
        }

        internal void RecordContextRecovery(CopilotContextWindowRecoveryInfo recovery)
        {
            ArgumentNullException.ThrowIfNull(recovery);
            CopilotAgentBudgetSnapshot snapshot;
            lock (_syncRoot)
            {
                var estimatedInputTokensBefore = Math.Max(0, recovery.EstimatedInputTokensBefore);
                var estimatedInputTokensAfter = Math.Clamp(
                    recovery.EstimatedInputTokensAfter,
                    0,
                    estimatedInputTokensBefore);
                _contextRecoveryCount = AddClamped(_contextRecoveryCount, 1);
                _contextRecoveryEstimatedInputTokensBefore = AddClamped(
                    _contextRecoveryEstimatedInputTokensBefore,
                    estimatedInputTokensBefore);
                _contextRecoveryEstimatedInputTokensAfter = AddClamped(
                    _contextRecoveryEstimatedInputTokensAfter,
                    estimatedInputTokensAfter);
                snapshot = CreateSnapshot();
            }
            PublishBudgetChanged(snapshot);
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
            var providerStopwatch = Stopwatch.StartNew();
            try
            {
                response = await base.GetResponseAsync(materializedMessages, options, cancellationToken);
            }
            catch (CopilotProviderConnectionRecoveryCancelledException)
            {
                RecordProviderCallDuration(ToMilliseconds(providerStopwatch.Elapsed));
                throw;
            }
            catch (Exception exception) when (CopilotContextWindowFailureClassifier.TryClassify(exception, out _))
            {
                RecordProviderCallDuration(ToMilliseconds(providerStopwatch.Elapsed));
                throw;
            }
            catch (Exception exception)
            {
                RecordProviderCallDuration(ToMilliseconds(providerStopwatch.Elapsed));
                RecordProviderInactivity(exception);
                CommitUsage(CopilotTokenUsage.Empty, estimatedInputTokens, requireEstimatedFloor: true);
                throw;
            }
            var providerDurationMs = ToMilliseconds(providerStopwatch.Elapsed);
            RecordProviderFirstResponse(providerDurationMs);
            RecordProviderCallDuration(providerDurationMs);
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
            long providerCallDurationTicks = 0;
            long providerInterChunkLatencyTicks = 0;
            var providerResponseStarted = false;
            var completed = false;
            IAsyncEnumerator<ChatResponseUpdate>? enumerator;
            var providerStopwatch = Stopwatch.StartNew();
            try
            {
                enumerator = await OpenStreamingAttemptAsync(materializedMessages, options, cancellationToken);
            }
            catch (CopilotProviderConnectionRecoveryCancelledException)
            {
                RecordProviderCallDuration(ToMilliseconds(providerStopwatch.Elapsed));
                throw;
            }
            catch (Exception exception) when (CopilotContextWindowFailureClassifier.TryClassify(exception, out _))
            {
                RecordProviderCallDuration(ToMilliseconds(providerStopwatch.Elapsed));
                throw;
            }
            catch (Exception exception)
            {
                RecordProviderCallDuration(ToMilliseconds(providerStopwatch.Elapsed));
                RecordProviderInactivity(exception);
                CommitUsage(CopilotTokenUsage.Empty, estimatedInputTokens, requireEstimatedFloor: true);
                throw;
            }
            providerCallDurationTicks = Math.Max(0, providerStopwatch.ElapsedTicks);

            if (enumerator == null)
            {
                RecordProviderCallDuration(ToMilliseconds(providerCallDurationTicks));
                CommitUsage(
                    usage,
                    EstimateTokens(materializedMessages, options, responseWeight));
                yield break;
            }

            await using (enumerator)
            {
                try
                {
                    while (true)
                    {
                        var update = enumerator.Current;
                        responseWeight += EstimateContentWeight(update.Contents);
                        usage = usage.MergeProgress(ExtractUsage(update.Contents));
                        if (CopilotProviderResponseContent.HasAny(update.Contents))
                        {
                            if (!providerResponseStarted)
                            {
                                providerResponseStarted = true;
                                RecordProviderFirstResponse(ToMilliseconds(providerCallDurationTicks));
                                RecordProviderStreamChunk(interChunkLatencyMs: null);
                            }
                            else
                            {
                                RecordProviderStreamChunk(
                                    ToMilliseconds(providerInterChunkLatencyTicks));
                            }
                            providerInterChunkLatencyTicks = 0;
                        }
                        yield return update;
                        providerStopwatch.Restart();
                        bool hasNext;
                        try
                        {
                            hasNext = await enumerator.MoveNextAsync();
                        }
                        catch (Exception exception)
                        {
                            RecordProviderInactivity(exception);
                            throw;
                        }
                        finally
                        {
                            providerCallDurationTicks = AddClamped(
                                providerCallDurationTicks,
                                providerStopwatch.ElapsedTicks);
                            if (providerResponseStarted)
                            {
                                providerInterChunkLatencyTicks = AddClamped(
                                    providerInterChunkLatencyTicks,
                                    providerStopwatch.ElapsedTicks);
                            }
                        }
                        if (!hasNext)
                            break;
                    }
                    completed = true;
                }
                finally
                {
                    RecordProviderCallDuration(ToMilliseconds(providerCallDurationTicks));
                    CommitUsage(
                        usage,
                        EstimateTokens(materializedMessages, options, responseWeight),
                        requireEstimatedFloor: !completed);
                }
            }
        }

        private async Task<IAsyncEnumerator<ChatResponseUpdate>?> OpenStreamingAttemptAsync(
            IReadOnlyList<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options,
            CancellationToken cancellationToken)
        {
            var enumerator = base.GetStreamingResponseAsync(messages, options, cancellationToken).GetAsyncEnumerator(cancellationToken);
            try
            {
                if (await enumerator.MoveNextAsync())
                    return enumerator;

                await enumerator.DisposeAsync();
                return null;
            }
            catch
            {
                try
                {
                    await enumerator.DisposeAsync();
                }
                catch
                {
                    // Preserve the provider failure used by context recovery.
                }
                throw;
            }
        }

        private void EnsureWithinContextWindow(int estimatedInputTokens)
        {
            lock (_syncRoot)
            {
                _peakEstimatedInputTokens = Math.Max(_peakEstimatedInputTokens, Math.Max(0, estimatedInputTokens));
                if (estimatedInputTokens <= _budget.InputBudgetTokens)
                    return;
                _usedEstimatedUsage = true;
            }
            throw new CopilotAgentContextWindowExceededException(estimatedInputTokens, _budget.InputBudgetTokens);
        }

        private bool TryBeginProviderCall(int estimatedInputTokens)
        {
            CopilotAgentBudgetSnapshot? notification = null;
            lock (_syncRoot)
            {
                var wouldExceedBudget = AddClamped(
                    _consumedTokens,
                    Math.Max(1, estimatedInputTokens)) > _budget.RequestTokenBudget;
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
                PublishBudgetObserver(_onBudgetExhausted, notification, "exhaustion");
            return false;
        }

        private void CommitUsage(CopilotTokenUsage actualUsage, int estimatedTokens, bool requireEstimatedFloor = false)
        {
            CopilotAgentBudgetSnapshot snapshot;
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
                _consumedTokens = AddClamped(_consumedTokens, consumedTokens);

                if (_consumedTokens >= _budget.RequestTokenBudget)
                    _budgetExhausted = true;
                snapshot = CreateSnapshot();
            }
            PublishBudgetChanged(snapshot);
        }

        private void PublishBudgetChanged(CopilotAgentBudgetSnapshot snapshot)
        {
            PublishBudgetObserver(_onBudgetChanged, snapshot, "update");
        }

        private static void PublishBudgetObserver(
            Action<CopilotAgentBudgetSnapshot>? observer,
            CopilotAgentBudgetSnapshot snapshot,
            string notificationKind)
        {
            try
            {
                observer?.Invoke(snapshot);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning(
                    "Copilot budget {0} observer failed: {1}",
                    notificationKind,
                    ex.GetType().Name);
            }
        }

        private void RecordProviderFirstResponse(long latencyMs)
        {
            var normalizedLatencyMs = Math.Max(0, latencyMs);
            lock (_syncRoot)
            {
                _providerResponseCount = AddClamped(_providerResponseCount, 1);
                _providerFirstResponseLatencyTotalMs = AddClamped(
                    _providerFirstResponseLatencyTotalMs,
                    normalizedLatencyMs);
                _providerFirstResponseLatencyMaxMs = Math.Max(
                    _providerFirstResponseLatencyMaxMs,
                    normalizedLatencyMs);
            }
        }

        private void RecordProviderCallDuration(long durationMs)
        {
            lock (_syncRoot)
            {
                _providerCallDurationTotalMs = AddClamped(
                    _providerCallDurationTotalMs,
                    durationMs);
            }
        }

        private void RecordProviderStreamChunk(long? interChunkLatencyMs)
        {
            lock (_syncRoot)
            {
                _providerStreamChunkCount = AddClamped(_providerStreamChunkCount, 1);
                if (!interChunkLatencyMs.HasValue)
                    return;

                var normalizedLatencyMs = Math.Max(0, interChunkLatencyMs.Value);
                _providerStreamInterChunkLatencyCount = AddClamped(
                    _providerStreamInterChunkLatencyCount,
                    1);
                _providerStreamInterChunkLatencyTotalMs = AddClamped(
                    _providerStreamInterChunkLatencyTotalMs,
                    normalizedLatencyMs);
                _providerStreamInterChunkLatencyMaxMs = Math.Max(
                    _providerStreamInterChunkLatencyMaxMs,
                    normalizedLatencyMs);
            }
        }

        private CopilotAgentBudgetSnapshot CreateSnapshot()
        {
            var reportedInputTokens = Math.Max(0, _usage.InputTokens);
            var reportedOutputTokens = Math.Max(0, _usage.OutputTokens);
            var reportedTotalTokens = (int)Math.Clamp(
                Math.Max(
                    (long)Math.Max(0, _usage.EffectiveTotalTokens),
                    (long)reportedInputTokens + reportedOutputTokens),
                0,
                int.MaxValue);
            var providerCalls = Math.Max(0, _providerCalls);
            var providerRetryCount = Math.Clamp(_providerRetryCount, 0, providerCalls);
            var providerFirstContentTimeoutCount = Math.Clamp(
                _providerFirstContentTimeoutCount,
                0,
                providerCalls);
            var providerStreamInactivityTimeoutCount = Math.Clamp(
                _providerStreamInactivityTimeoutCount,
                0,
                providerCalls - providerFirstContentTimeoutCount);
            var providerResponseCount = Math.Clamp(_providerResponseCount, 0, providerCalls);
            var providerFirstResponseLatencyTotalMs = providerResponseCount > 0
                ? Math.Max(0, _providerFirstResponseLatencyTotalMs)
                : 0;
            var providerStreamChunkCount = providerResponseCount > 0
                ? Math.Max(0, _providerStreamChunkCount)
                : 0;
            var providerStreamInterChunkLatencyCount = Math.Clamp(
                _providerStreamInterChunkLatencyCount,
                0,
                Math.Max(0, providerStreamChunkCount - 1));
            var providerStreamInterChunkLatencyTotalMs = providerStreamInterChunkLatencyCount > 0
                ? Math.Max(0, _providerStreamInterChunkLatencyTotalMs)
                : 0;
            return new CopilotAgentBudgetSnapshot
            {
                CompactionEnabled = true,
                ContextWindowTokens = _budget.ContextWindowTokens,
                InputBudgetTokens = _budget.InputBudgetTokens,
                RequestTokenBudget = _budget.RequestTokenBudget,
                ConsumedTokens = Math.Max(0, _consumedTokens),
                ProviderCalls = providerCalls,
                PeakEstimatedInputTokens = Math.Max(0, _peakEstimatedInputTokens),
                ProviderRetryCount = providerRetryCount,
                ProviderRateLimitRetryCount = Math.Clamp(
                    _providerRateLimitRetryCount,
                    0,
                    providerRetryCount),
                ProviderRetryDelayMs = providerRetryCount > 0
                    ? Math.Max(0, _providerRetryDelayMs)
                    : 0,
                ProviderFirstContentTimeoutCount = providerFirstContentTimeoutCount,
                ProviderStreamInactivityTimeoutCount =
                    providerStreamInactivityTimeoutCount,
                ProviderResponseCount = providerResponseCount,
                ProviderFirstResponseLatencyTotalMs = providerFirstResponseLatencyTotalMs,
                ProviderFirstResponseLatencyMaxMs = Math.Clamp(
                    _providerFirstResponseLatencyMaxMs,
                    0,
                    providerFirstResponseLatencyTotalMs),
                ProviderCallDurationTotalMs = Math.Max(
                    providerFirstResponseLatencyTotalMs,
                    _providerCallDurationTotalMs),
                ProviderStreamChunkCount = providerStreamChunkCount,
                ProviderStreamInterChunkLatencyCount = providerStreamInterChunkLatencyCount,
                ProviderStreamInterChunkLatencyTotalMs = providerStreamInterChunkLatencyTotalMs,
                ProviderStreamInterChunkLatencyMaxMs = Math.Clamp(
                    _providerStreamInterChunkLatencyMaxMs,
                    0,
                    providerStreamInterChunkLatencyTotalMs),
                ContextRecoveryCount = Math.Max(0, _contextRecoveryCount),
                ContextRecoveryEstimatedInputTokensBefore = Math.Max(
                    0,
                    _contextRecoveryEstimatedInputTokensBefore),
                ContextRecoveryEstimatedInputTokensAfter = Math.Clamp(
                    _contextRecoveryEstimatedInputTokensAfter,
                    0,
                    Math.Max(0, _contextRecoveryEstimatedInputTokensBefore)),
                ReportedInputTokens = reportedInputTokens,
                ReportedOutputTokens = reportedOutputTokens,
                ReportedTotalTokens = reportedTotalTokens,
                ReportedCachedInputTokens = reportedInputTokens > 0
                    && _usage.CachedInputTokens.HasValue
                    ? _usage.EffectiveCachedInputTokens
                    : null,
                UsedEstimatedUsage = _usedEstimatedUsage,
                BudgetExhausted = _budgetExhausted,
            };
        }

    }
}
