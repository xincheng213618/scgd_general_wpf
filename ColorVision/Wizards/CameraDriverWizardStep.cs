using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.ToolPlugins.CameraDriver;
using ColorVision.UI;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ColorVision.Wizards
{
    public sealed class CameraDriverWizardStep : WizardStepBase
    {
        private readonly ICameraDriverInstallationService _service;
        private readonly Action _openWindow;
        private readonly RelayCommand _command;
        private CameraDriverInstallationStatus? _status;
        private bool _isBusy;
        private bool _reviewCompleted;
        private string _errorMessage = string.Empty;

        public CameraDriverWizardStep() : this(new CameraDriverInstallationService(), CameraDriverWindow.ShowWindow) { }

        internal CameraDriverWizardStep(ICameraDriverInstallationService service, Action openWindow)
        {
            _service = service;
            _openWindow = openWindow;
            _command = new RelayCommand(_ => _ = ExecuteAsync(), _ => !IsBusy);
        }

        public override int Order => -850;
        public override string Header => CameraDriverText.Get("Header");
        public override bool RunsBeforeInitializers => true;
        public override bool IsBusy => _isBusy;
        public override bool HasError => !string.IsNullOrEmpty(_errorMessage);
        public override string ErrorMessage => _errorMessage;
        public override ICommand Command => _command;
        public override bool ConfigurationStatus { get => _reviewCompleted || _status?.IsInstalled == true; set { } }
        public override string Description => CameraDriverText.Get("Intro") + Environment.NewLine + Environment.NewLine + CameraDriverText.Describe(_status);
        public override string ActionText => CameraDriverText.Get("ManageAction");

        public override async Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            if (IsBusy) return;
            _isBusy = true;
            _status = null;
            _errorMessage = string.Empty;
            NotifyStateChanged();
            try { _status = await _service.QueryAsync(cancellationToken); }
            catch (Exception ex) { _errorMessage = ex.Message; }
            finally { _isBusy = false; NotifyStateChanged(); }
        }

        public override Task<bool> ApplyAsync(CancellationToken cancellationToken = default)
        {
            if (IsBusy) return Task.FromResult(false);
            // Next skips this optional step without downloading or starting an installer.
            _reviewCompleted = true;
            _errorMessage = string.Empty;
            NotifyStateChanged();
            return Task.FromResult(true);
        }

        internal async Task ExecuteAsync()
        {
            if (IsBusy) return;
            _isBusy = true;
            NotifyStateChanged();
            try { _openWindow(); }
            catch (Exception ex) { _errorMessage = ex.Message; return; }
            finally { _isBusy = false; NotifyStateChanged(); }
            await RefreshAsync();
        }

        private void NotifyStateChanged()
        {
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(CanContinue));
            OnPropertyChanged(nameof(ConfigurationStatus));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(ErrorMessage));
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
