using ColorVision.UI;

namespace ColorVision.Wizards
{
    public class WizardWindowConfig:WindowConfig 
    {
        public static WizardWindowConfig Instance => ConfigService.Instance.GetRequiredService<WizardWindowConfig>();

        public bool WizardCompletionKey { get => _WizardCompletionKey; set { _WizardCompletionKey = value; NotifyPropertyChanged(); } }
        private bool _WizardCompletionKey;

        public WizardShowType WizardShowType { get => _WizardShowType; set { _WizardShowType = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(IsList)); } }
        private WizardShowType _WizardShowType;

        public bool IsList => WizardShowType == WizardShowType.List;
    }
}
