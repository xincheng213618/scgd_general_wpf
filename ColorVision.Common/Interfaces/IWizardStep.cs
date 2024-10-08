using ColorVision.Common.MVVM;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;

namespace ColorVision.UI
{
    public interface IWizardStep
    {
        public int Order { get; }
        public string Header { get; }
        public RelayCommand Command { get; }

        public string Description { get; }
    }

    public abstract class WizardStepBase : ViewModelBase, IWizardStep
    {
        public abstract string Header { get; }
        public abstract int Order { get; }

        public virtual RelayCommand Command => new(A => Execute(), b => AccessControl.Check(Execute));
        public abstract string Description { get; }

        public virtual void Execute()
        {

        }
    }
}
