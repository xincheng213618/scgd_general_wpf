using ColorVision.Common.MVVM;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

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

        [Browsable(false)]
        public ObservableCollection<CopilotAgentSkillOverrideConfig> SkillOverrides { get; set; } = new();

        public bool EnsureValid()
        {
            var normalizedContextWindowTokens = Math.Clamp(ContextWindowTokens, CopilotAgentTokenBudget.MinimumContextWindowTokens, CopilotAgentTokenBudget.MaximumContextWindowTokens);
            var normalizedRequestTokenBudget = Math.Clamp(RequestTokenBudget, CopilotAgentRunBudget.MinimumRequestTokenBudget, CopilotAgentRunBudget.MaximumRequestTokenBudget);
            var normalizedMaxToolCalls = Math.Clamp(MaxToolCalls, CopilotAgentRunBudget.MinimumToolCalls, CopilotAgentRunBudget.MaximumToolCalls);
            var normalizedMaxAgentPasses = Math.Clamp(MaxAgentPasses, CopilotAgentRunBudget.MinimumAgentPasses, CopilotAgentRunBudget.MaximumAgentPasses);
            var normalizedTimeoutSeconds = Math.Clamp(TimeoutSeconds, (int)CopilotAgentRunBudget.MinimumTotalDuration.TotalSeconds, (int)CopilotAgentRunBudget.MaximumTotalDuration.TotalSeconds);
            var normalizedShell = Enum.IsDefined(PreferredShell) ? PreferredShell : CopilotShellKind.Auto;
            SkillOverrides ??= new ObservableCollection<CopilotAgentSkillOverrideConfig>();
            var normalizedSkillOverrides = CopilotAgentSkillOverrideConfig.Normalize(SkillOverrides);
            var skillOverridesChanged = !SkillOverrides
                .Select(item => (item?.Name ?? string.Empty, item?.State ?? CopilotAgentSkillOverrideState.Auto))
                .SequenceEqual(normalizedSkillOverrides.Select(item => (item.Name, item.State)));
            var changed = normalizedContextWindowTokens != ContextWindowTokens
                || normalizedRequestTokenBudget != RequestTokenBudget
                || normalizedMaxToolCalls != MaxToolCalls
                || normalizedMaxAgentPasses != MaxAgentPasses
                || normalizedTimeoutSeconds != TimeoutSeconds
                || normalizedShell != PreferredShell
                || skillOverridesChanged;
            ContextWindowTokens = normalizedContextWindowTokens;
            RequestTokenBudget = normalizedRequestTokenBudget;
            MaxToolCalls = normalizedMaxToolCalls;
            MaxAgentPasses = normalizedMaxAgentPasses;
            TimeoutSeconds = normalizedTimeoutSeconds;
            PreferredShell = normalizedShell;
            if (skillOverridesChanged)
            {
                SkillOverrides.Clear();
                foreach (var item in normalizedSkillOverrides)
                    SkillOverrides.Add(item);
            }
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
                SkillOverrides = new ObservableCollection<CopilotAgentSkillOverrideConfig>(SkillOverrides.Select(item => item.Clone())),
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

        public IReadOnlyDictionary<string, CopilotAgentSkillOverrideState> CreateSkillOverrideSnapshot()
        {
            return CopilotAgentSkillOverrideConfig.Normalize(SkillOverrides)
                .ToDictionary(item => item.Name, item => item.State, StringComparer.OrdinalIgnoreCase);
        }
    }
}
