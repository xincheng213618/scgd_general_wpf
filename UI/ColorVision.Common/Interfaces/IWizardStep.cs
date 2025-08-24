using ColorVision.Common.MVVM;
using System.Windows.Input;

namespace ColorVision.UI
{
    public interface IWizardStep
    {
        int Order { get; }
        string Header { get; }
        ICommand? Command { get; }
        string Description { get; }
        bool ConfigurationStatus { get; set; }

    }

    public abstract class WizardStepBase : ViewModelBase, IWizardStep
    {
        public abstract string Header { get; }
        public abstract int Order { get; }

        public virtual ICommand? Command => new RelayCommand(A => Execute());
        public abstract string Description { get; }

        public virtual bool ConfigurationStatus { get => _ConfigurationStatus; set { _ConfigurationStatus = value; OnPropertyChanged(); } }
        private bool _ConfigurationStatus = true;

        public virtual void Execute()
        {

        }
    }
}
