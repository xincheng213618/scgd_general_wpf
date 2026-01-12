using ColorVision.UI;

 
namespace ColorVision.UI.Desktop.Wizards
{
    public class WizardWindowConfig:WindowConfig 
    {
        public static WizardWindowConfig Instance => ConfigService.Instance.GetRequiredService<WizardWindowConfig>();

        public bool WizardCompletionKey { get => _WizardCompletionKey; set { _WizardCompletionKey = value; OnPropertyChanged(); } }
        private bool _WizardCompletionKey;
    }
}
