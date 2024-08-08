using ColorVision.Common.MVVM;

namespace ColorVision.UI
{
    public interface IWizardStep
    {
        public int Order { get; }
        public string Title { get; }
        public string Description { get; }
        public RelayCommand? RelayCommand { get; }
    }
}
