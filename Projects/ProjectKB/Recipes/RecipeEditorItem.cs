using ColorVision.Common.MVVM;

namespace ProjectKB
{
    public class RecipeEditorItem : ViewModelBase
    {
        public RecipeEditorItem(string templateName, KBRecipeConfig config, bool isCurrentTemplate)
        {
            TemplateName = templateName;
            Config = config;
            IsCurrentTemplate = isCurrentTemplate;
        }

        public string TemplateName { get; }

        public KBRecipeConfig Config { get; }

        public bool IsCurrentTemplate { get => _isCurrentTemplate; set { _isCurrentTemplate = value; OnPropertyChanged(); OnPropertyChanged(nameof(CurrentText)); } }
        private bool _isCurrentTemplate;

        public bool HasLimit => RecipeManager.HasAnyLimit(Config);

        public string StatusText => HasLimit ? "已启用" : "未启用";

        public string CurrentText => IsCurrentTemplate ? "当前" : string.Empty;

        public void RefreshStatus()
        {
            OnPropertyChanged(nameof(HasLimit));
            OnPropertyChanged(nameof(StatusText));
        }
    }
}