using ColorVision.Common.MVVM;
using System;

namespace ColorVision.Copilot
{
    public sealed class CopilotExternalMcpClientStatusItem
    {
        public string ServerName { get; init; } = string.Empty;

        public string Endpoint { get; init; } = string.Empty;

        public string StateText { get; init; } = string.Empty;

        public string DetailText { get; init; } = string.Empty;

        public string CheckedText { get; init; } = string.Empty;
    }

    public sealed class CopilotConnectProviderOption
    {
        public string GroupName { get; init; } = string.Empty;

        public string IconText { get; init; } = string.Empty;

        public string Label { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public string BadgeText { get; init; } = string.Empty;

        public string SearchKeywords { get; init; } = string.Empty;

        public CopilotVendorType VendorType { get; init; }

        public bool HasBadge => !string.IsNullOrWhiteSpace(BadgeText);

        public bool Matches(string? searchText)
        {
            var query = (searchText ?? string.Empty).Trim();
            if (query.Length == 0)
                return true;

            return Label.Contains(query, StringComparison.OrdinalIgnoreCase)
                || Description.Contains(query, StringComparison.OrdinalIgnoreCase)
                || BadgeText.Contains(query, StringComparison.OrdinalIgnoreCase)
                || SearchKeywords.Contains(query, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed record CopilotAgentSkillOverrideOption(
        CopilotAgentSkillOverrideState State,
        string Label);

    public sealed class CopilotAgentSkillSetting : ViewModelBase
    {
        private readonly Action _changed;

        public CopilotAgentSkillSetting(
            string name,
            CopilotAgentSkillOverrideState state,
            CopilotAgentSkillUsageEntry? usage,
            bool isHistoricalExplicitOnly,
            Action changed,
            string? skillFilePath = null)
        {
            Name = name;
            SkillFilePath = CopilotAgentSkillOverrideConfig.NormalizeSkillFilePath(skillFilePath);
            _state = state;
            _changed = changed ?? throw new ArgumentNullException(nameof(changed));
            UpdateUsage(usage, isHistoricalExplicitOnly);
        }

        public string Name { get; }

        public string SkillFilePath { get; }

        public bool HasExactPath => SkillFilePath.Length > 0;

        internal string Identity => HasExactPath ? "path\0" + SkillFilePath : "name\0" + Name;

        public CopilotAgentSkillOverrideState State
        {
            get => _state;
            set
            {
                var normalized = Enum.IsDefined(value) ? value : CopilotAgentSkillOverrideState.Auto;
                if (SetProperty(ref _state, normalized))
                    _changed();
            }
        }
        private CopilotAgentSkillOverrideState _state;

        public bool IsTracked
        {
            get => _isTracked;
            private set => SetProperty(ref _isTracked, value);
        }
        private bool _isTracked;

        public string UsageSummary
        {
            get => _usageSummary;
            private set => SetProperty(ref _usageSummary, value ?? string.Empty);
        }
        private string _usageSummary = string.Empty;

        public void UpdateUsage(CopilotAgentSkillUsageEntry? usage, bool isHistoricalExplicitOnly)
        {
            IsTracked = usage != null;
            if (usage == null)
            {
                UsageSummary = HasExactPath
                    ? "精确路径：" + SkillFilePath
                    : "尚无本地使用证据；发现该 Skill 时仍会应用此覆盖设置。";
                return;
            }

            UsageSummary = $"已加载 {usage.LoadedRuns}/{usage.SelectedRuns} 次选中运行（{usage.LoadRate:P0}）；连续未加载 {usage.ConsecutiveSelectedWithoutLoad}/{CopilotAgentSkillUsageStore.LowUseConsecutiveMissThreshold}"
                + (isHistoricalExplicitOnly ? " · 自动策略当前解析为仅显式调用" : string.Empty);
        }
    }
}
