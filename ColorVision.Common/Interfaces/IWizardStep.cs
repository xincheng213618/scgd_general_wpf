using ColorVision.Common.MVVM;

namespace ColorVision.UI
{
    public interface IWizardStep
    {
        public int Order { get; }
        public string Header { get; }
        public RelayCommand Command { get; }
    }
}
