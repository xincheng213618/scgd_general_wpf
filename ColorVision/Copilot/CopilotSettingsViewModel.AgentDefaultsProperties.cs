#pragma warning disable CA1822
using System;
using System.Linq;
using System.Windows.Input;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotSettingsViewModel
    {
        public CopilotShellKind PreferredShell
        {
            get => _preferredShell;
            set
            {
                if (SetProperty(ref _preferredShell, value) && _isReadyForUserChanges)
                    MarkSettingsPending("Default Agent shell changed. Click Apply or Save to use it.");
            }
        }
        private CopilotShellKind _preferredShell = CopilotShellKind.Auto;

        public int AgentContextWindowTokens
        {
            get => _agentContextWindowTokens;
            set
            {
                var normalized = Math.Clamp(value, CopilotAgentTokenBudget.MinimumContextWindowTokens, CopilotAgentTokenBudget.MaximumContextWindowTokens);
                if (SetProperty(ref _agentContextWindowTokens, normalized) && _isReadyForUserChanges)
                    MarkSettingsPending("Agent context-window budget changed. Click Apply or Save to use it.");
            }
        }
        private int _agentContextWindowTokens = CopilotAgentDefaultsConfig.DefaultContextWindowTokens;

        public bool AutoCompactConversationHistory
        {
            get => _autoCompactConversationHistory;
            set
            {
                if (SetProperty(ref _autoCompactConversationHistory, value) && _isReadyForUserChanges)
                    MarkSettingsPending("Conversation auto-compaction changed. Click Apply or Save to use it.");
            }
        }
        private bool _autoCompactConversationHistory = true;

        public int AutoCompactThresholdPercent
        {
            get => _autoCompactThresholdPercent;
            set
            {
                var normalized = Math.Clamp(
                    value,
                    CopilotAgentDefaultsConfig.MinimumAutoCompactThresholdPercent,
                    CopilotAgentDefaultsConfig.MaximumAutoCompactThresholdPercent);
                if (SetProperty(ref _autoCompactThresholdPercent, normalized) && _isReadyForUserChanges)
                    MarkSettingsPending("Conversation auto-compaction threshold changed. Click Apply or Save to use it.");
            }
        }
        private int _autoCompactThresholdPercent = CopilotAgentDefaultsConfig.DefaultAutoCompactThresholdPercent;

        public string AutoCompactInstructions
        {
            get => _autoCompactInstructions;
            set
            {
                var normalized = value ?? string.Empty;
                if (normalized.Length > CopilotAgentDefaultsConfig.MaximumAutoCompactInstructionsCharacters)
                {
                    var length = CopilotAgentDefaultsConfig.MaximumAutoCompactInstructionsCharacters;
                    if (char.IsHighSurrogate(normalized[length - 1]))
                        length--;
                    normalized = normalized[..length];
                }
                if (SetProperty(ref _autoCompactInstructions, normalized) && _isReadyForUserChanges)
                    MarkSettingsPending("Automatic compaction focus changed. Click Apply or Save to use it.");
            }
        }
        private string _autoCompactInstructions = string.Empty;

        public int AgentRequestTokenBudget
        {
            get => _agentRequestTokenBudget;
            set
            {
                var normalized = Math.Clamp(value, CopilotAgentRunBudget.MinimumRequestTokenBudget, CopilotAgentRunBudget.MaximumRequestTokenBudget);
                if (SetProperty(ref _agentRequestTokenBudget, normalized) && _isReadyForUserChanges)
                    MarkSettingsPending("Agent request-token budget changed. Click Apply or Save to use it.");
            }
        }
        private int _agentRequestTokenBudget = CopilotAgentDefaultsConfig.DefaultRequestTokenBudget;

        public int MaxAgentToolCalls
        {
            get => _maxAgentToolCalls;
            set
            {
                var normalized = Math.Clamp(value, CopilotAgentRunBudget.MinimumToolCalls, CopilotAgentRunBudget.MaximumToolCalls);
                if (SetProperty(ref _maxAgentToolCalls, normalized) && _isReadyForUserChanges)
                    MarkSettingsPending("Agent tool-call budget changed. Click Apply or Save to use it.");
            }
        }
        private int _maxAgentToolCalls = CopilotAgentDefaultsConfig.DefaultMaxToolCalls;

        public int MaxAgentPasses
        {
            get => _maxAgentPasses;
            set
            {
                var normalized = Math.Clamp(value, CopilotAgentRunBudget.MinimumAgentPasses, CopilotAgentRunBudget.MaximumAgentPasses);
                if (SetProperty(ref _maxAgentPasses, normalized) && _isReadyForUserChanges)
                    MarkSettingsPending("Agent pass budget changed. Click Apply or Save to use it.");
            }
        }
        private int _maxAgentPasses = CopilotAgentDefaultsConfig.DefaultMaxAgentPasses;

        public int AgentTimeoutSeconds
        {
            get => _agentTimeoutSeconds;
            set
            {
                var normalized = Math.Clamp(value, (int)CopilotAgentRunBudget.MinimumTotalDuration.TotalSeconds, (int)CopilotAgentRunBudget.MaximumTotalDuration.TotalSeconds);
                if (SetProperty(ref _agentTimeoutSeconds, normalized) && _isReadyForUserChanges)
                    MarkSettingsPending("Agent timeout changed. Click Apply or Save to use it.");
            }
        }
        private int _agentTimeoutSeconds = CopilotAgentDefaultsConfig.DefaultTimeoutSeconds;

        public string NewAgentSkillName
        {
            get => _newAgentSkillName;
            set
            {
                if (!SetProperty(ref _newAgentSkillName, value ?? string.Empty))
                    return;
                OnPropertyChanged(nameof(CanAddAgentSkillOverride));
                CommandManager.InvalidateRequerySuggested();
            }
        }
        private string _newAgentSkillName = string.Empty;

        public bool CanAddAgentSkillOverride
        {
            get
            {
                var name = CopilotAgentSkillOverrideConfig.NormalizeName(NewAgentSkillName);
                return name.Length > 0
                    && AgentSkillSettings.Count(setting => setting.State != CopilotAgentSkillOverrideState.Auto) < CopilotAgentSkillOverrideConfig.MaxEntries
                    && !AgentSkillSettings.Any(setting => string.Equals(setting.Name, name, StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}
