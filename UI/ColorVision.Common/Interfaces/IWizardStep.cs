using ColorVision.Common.MVVM;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
        bool IsRequired => false;
        bool IsBusy => false;
        bool HasError => false;
        string ErrorMessage => string.Empty;
        string ActionText => string.Empty;
        bool CanContinue => !IsBusy && (!IsRequired || ConfigurationStatus);
        Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    }

    public sealed class WizardInitializationContext
    {
        public WizardInitializationContext(Window owner, bool isFirstRun)
        {
            Owner = owner;
            IsFirstRun = isFirstRun;
        }

        public Window Owner { get; }
        public bool IsFirstRun { get; }
        public bool SkipRequested { get; private set; }

        public void RequestSkipWizard()
        {
            SkipRequested = true;
        }
    }

    public interface IWizardInitializer
    {
        int Order { get; }
        void Initialize(WizardInitializationContext context);
    }

    public abstract class WizardStepBase : ViewModelBase, IWizardStep
    {
        public abstract string Header { get; }
        public abstract int Order { get; }

        public virtual ICommand? Command => new RelayCommand(A => Execute());
        public abstract string Description { get; }

        public virtual bool ConfigurationStatus
        {
            get => _ConfigurationStatus;
            set
            {
                _ConfigurationStatus = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanContinue));
            }
        }
        private bool _ConfigurationStatus = true;

        public virtual bool IsRequired => false;
        public virtual bool IsBusy => false;
        public virtual bool HasError => false;
        public virtual string ErrorMessage => string.Empty;
        public virtual string ActionText => string.Empty;
        public virtual bool CanContinue => !IsBusy && (!IsRequired || ConfigurationStatus);

        public virtual Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public virtual void Execute()
        {

        }
    }
}
