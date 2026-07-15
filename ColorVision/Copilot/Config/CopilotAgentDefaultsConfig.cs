using ColorVision.Common.MVVM;
using System;
using System.ComponentModel;

namespace ColorVision.Copilot
{
    public sealed class CopilotAgentDefaultsConfig : ViewModelBase
    {
        public const int DefaultContextWindowTokens = CopilotAgentTokenBudget.DefaultContextWindowTokens;
        public const int DefaultRequestTokenBudget = CopilotAgentRunBudget.MaximumRequestTokenBudget;
        public const int DefaultMaxToolCalls = 128;
        public const int DefaultMaxAgentPasses = 32;
        public const int DefaultTimeoutSeconds = 7_200;

        [Browsable(false)]
        public int ContextWindowTokens
        {
            get => _contextWindowTokens;
            set => SetProperty(ref _contextWindowTokens, Math.Clamp(value, CopilotAgentTokenBudget.MinimumContextWindowTokens, CopilotAgentTokenBudget.MaximumContextWindowTokens));
        }
        private int _contextWindowTokens = DefaultContextWindowTokens;

        [Browsable(false)]
        public int RequestTokenBudget
        {
            get => _requestTokenBudget;
            set => SetProperty(ref _requestTokenBudget, Math.Clamp(value, CopilotAgentRunBudget.MinimumRequestTokenBudget, CopilotAgentRunBudget.MaximumRequestTokenBudget));
        }
        private int _requestTokenBudget = DefaultRequestTokenBudget;

        [Browsable(false)]
        public int MaxToolCalls
        {
            get => _maxToolCalls;
            set => SetProperty(ref _maxToolCalls, Math.Clamp(value, CopilotAgentRunBudget.MinimumToolCalls, CopilotAgentRunBudget.MaximumToolCalls));
        }
        private int _maxToolCalls = DefaultMaxToolCalls;

        [Browsable(false)]
        public int MaxAgentPasses
        {
            get => _maxAgentPasses;
            set => SetProperty(ref _maxAgentPasses, Math.Clamp(value, CopilotAgentRunBudget.MinimumAgentPasses, CopilotAgentRunBudget.MaximumAgentPasses));
        }
        private int _maxAgentPasses = DefaultMaxAgentPasses;

        [Browsable(false)]
        public int TimeoutSeconds
        {
            get => _timeoutSeconds;
            set => SetProperty(ref _timeoutSeconds, Math.Clamp(value, (int)CopilotAgentRunBudget.MinimumTotalDuration.TotalSeconds, (int)CopilotAgentRunBudget.MaximumTotalDuration.TotalSeconds));
        }
        private int _timeoutSeconds = DefaultTimeoutSeconds;

        [Browsable(false)]
        public CopilotShellKind PreferredShell
        {
            get => _preferredShell;
            set => SetProperty(ref _preferredShell, Enum.IsDefined(value) ? value : CopilotShellKind.Auto);
        }
        private CopilotShellKind _preferredShell = CopilotShellKind.Auto;

        public bool EnsureValid()
        {
            var normalizedContextWindowTokens = Math.Clamp(ContextWindowTokens, CopilotAgentTokenBudget.MinimumContextWindowTokens, CopilotAgentTokenBudget.MaximumContextWindowTokens);
            var normalizedRequestTokenBudget = Math.Clamp(RequestTokenBudget, CopilotAgentRunBudget.MinimumRequestTokenBudget, CopilotAgentRunBudget.MaximumRequestTokenBudget);
            var normalizedMaxToolCalls = Math.Clamp(MaxToolCalls, CopilotAgentRunBudget.MinimumToolCalls, CopilotAgentRunBudget.MaximumToolCalls);
            var normalizedMaxAgentPasses = Math.Clamp(MaxAgentPasses, CopilotAgentRunBudget.MinimumAgentPasses, CopilotAgentRunBudget.MaximumAgentPasses);
            var normalizedTimeoutSeconds = Math.Clamp(TimeoutSeconds, (int)CopilotAgentRunBudget.MinimumTotalDuration.TotalSeconds, (int)CopilotAgentRunBudget.MaximumTotalDuration.TotalSeconds);
            var normalizedShell = Enum.IsDefined(PreferredShell) ? PreferredShell : CopilotShellKind.Auto;
            var changed = normalizedContextWindowTokens != ContextWindowTokens
                || normalizedRequestTokenBudget != RequestTokenBudget
                || normalizedMaxToolCalls != MaxToolCalls
                || normalizedMaxAgentPasses != MaxAgentPasses
                || normalizedTimeoutSeconds != TimeoutSeconds
                || normalizedShell != PreferredShell;
            ContextWindowTokens = normalizedContextWindowTokens;
            RequestTokenBudget = normalizedRequestTokenBudget;
            MaxToolCalls = normalizedMaxToolCalls;
            MaxAgentPasses = normalizedMaxAgentPasses;
            TimeoutSeconds = normalizedTimeoutSeconds;
            PreferredShell = normalizedShell;
            return changed;
        }

        public CopilotAgentDefaultsConfig Clone()
        {
            return new CopilotAgentDefaultsConfig
            {
                ContextWindowTokens = ContextWindowTokens,
                RequestTokenBudget = RequestTokenBudget,
                MaxToolCalls = MaxToolCalls,
                MaxAgentPasses = MaxAgentPasses,
                TimeoutSeconds = TimeoutSeconds,
                PreferredShell = PreferredShell,
            };
        }

        public CopilotAgentRunBudgetDefaults CreateRunBudgetDefaults()
        {
            return new CopilotAgentRunBudgetDefaults
            {
                ContextWindowTokens = ContextWindowTokens,
                RequestTokenBudget = RequestTokenBudget,
                MaxToolCalls = MaxToolCalls,
                MaxAgentPasses = MaxAgentPasses,
                TotalDuration = TimeSpan.FromSeconds(TimeoutSeconds),
            };
        }
    }
}
