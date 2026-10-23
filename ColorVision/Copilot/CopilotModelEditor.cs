using ColorVision.Common.MVVM;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ColorVision.Copilot
{
    public sealed class CopilotModelEditor : ViewModelBase
    {
        private readonly CopilotProfileConfig _initial;
        public CopilotModelEditor(CopilotProfileConfig draft, IReadOnlyList<string> presets)
        {
            Draft = draft;
            _initial = draft.Clone();
            ModelPresets = presets;
            _modelId = draft.Model;
            _smart = draft.UseModelDefaults;
            ReadValues();
        }
        public CopilotProfileConfig Draft { get; }
        public IReadOnlyList<string> ModelPresets { get; }
        public bool CanEditOutputLimit => !Draft.IsLocalCodex;
        public string OutputLimitHint => Draft.IsLocalCodex ? "本地 Codex 的实际输出上限由 Codex 管理；此数值仅用于宿主预算预留。" : string.Empty;
        private string _modelId;
        public string ModelId
        {
            get => _modelId;
            set
            {
                if (!SetProperty(ref _modelId, value ?? string.Empty)) return;
                Draft.Model = _modelId;
                if (SmartConfiguration) ApplyDefaults();
            }
        }
        private bool _smart;
        public bool SmartConfiguration
        {
            get => _smart;
            set
            {
                if (!SetProperty(ref _smart, value)) return;
                Draft.UseModelDefaults = value;
                if (value) ApplyDefaults();
                OnPropertyChanged(nameof(CanEditCapabilities));
                OnPropertyChanged(nameof(DefaultsHint));
            }
        }
        public bool CanEditCapabilities => !SmartConfiguration || CopilotModelDefaults.Find(Draft) == null;
        public string DefaultsHint => CopilotModelDefaults.Find(Draft) == null
            ? "此模型没有内置参数，可手动设置；上下文填 0 时使用 Agent 默认值。"
            : "已按模型预设填写上下文与视觉能力，可关闭智能配置后调整。";
        private string _context = string.Empty;
        public string ContextWindowText { get => _context; set => SetProperty(ref _context, value); }
        private string _output = string.Empty;
        public string MaxOutputText { get => _output; set => SetProperty(ref _output, value); }
        private bool _vision;
        public bool SupportsImages { get => _vision; set => SetProperty(ref _vision, value); }
        private string _error = string.Empty;
        public string Error { get => _error; set => SetProperty(ref _error, value); }

        private void ReadValues()
        {
            ContextWindowText = Draft.ContextWindowTokens.ToString(CultureInfo.InvariantCulture);
            MaxOutputText = Draft.MaxTokens.ToString(CultureInfo.InvariantCulture);
            SupportsImages = Draft.SupportsImageInput;
            OnPropertyChanged(nameof(CanEditCapabilities));
            OnPropertyChanged(nameof(DefaultsHint));
        }
        private void ApplyDefaults() { CopilotModelDefaults.Apply(Draft); ReadValues(); }
        public void Reset()
        {
            ModelId = _initial.Model;
            SmartConfiguration = _initial.UseModelDefaults;
            Draft.ContextWindowTokens = _initial.ContextWindowTokens;
            Draft.MaxTokens = _initial.MaxTokens;
            Draft.SupportsImageInput = _initial.SupportsImageInput;
            Draft.FirstContentTimeoutSeconds = _initial.FirstContentTimeoutSeconds;
            Draft.StreamingInactivityTimeoutSeconds = _initial.StreamingInactivityTimeoutSeconds;
            ReadValues();
            Error = string.Empty;
        }
        public bool Validate()
        {
            if (!int.TryParse(ContextWindowText, out var context) || context < 0 || context > CopilotAgentTokenBudget.MaximumContextWindowTokens
                || (context > 0 && context < CopilotAgentTokenBudget.MinimumContextWindowTokens))
            { Error = "上下文窗口应为 0，或 32768–2000000 之间的整数。"; return false; }
            if (!int.TryParse(MaxOutputText, out var output) || output < 32 || output > CopilotProfileConfig.MaximumMaxTokens)
            { Error = "最大输出 Token 应为 32–1048576 之间的整数。"; return false; }
            if (context > 0 && output >= context)
            { Error = "最大输出 Token 必须小于上下文窗口。"; return false; }
            Draft.Model = ModelId;
            Draft.UseModelDefaults = SmartConfiguration;
            Draft.ContextWindowTokens = context;
            Draft.MaxTokens = output;
            Draft.SupportsImageInput = SupportsImages;
            Error = string.Empty;
            return true;
        }
    }
}
