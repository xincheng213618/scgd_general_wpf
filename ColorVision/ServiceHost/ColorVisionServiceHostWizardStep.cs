using ColorVision.Common.MVVM;
using ColorVision.UI;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ColorVision.ServiceHost
{
    public sealed class ColorVisionServiceHostWizardStep : WizardStepBase
    {
        private readonly RelayCommand _command;
        private ServiceHostStatus? _status;
        private bool _isBusy;
        private string _errorMessage = string.Empty;

        public ColorVisionServiceHostWizardStep()
        {
            _command = new RelayCommand(_ => _ = ExecuteAsync(), _ => !IsBusy);
        }

        public override int Order => -1000;
        public override string Header => GetResource("ServiceHostWizardHeader");
        public override bool IsRequired => true;
        public override bool IsBusy => _isBusy;
        public override bool HasError => !string.IsNullOrWhiteSpace(_errorMessage);
        public override string ErrorMessage => _errorMessage;
        public override bool ConfigurationStatus
        {
            get => _status != null && ColorVisionServiceHostManager.IsReadyForPackagedVersion(_status);
            set { }
        }
        public override bool CanContinue => !IsBusy && ConfigurationStatus;
        public override ICommand? Command => _command;

        public override string ActionText
        {
            get
            {
                if (IsBusy)
                    return GetResource("ServiceHostWizardWorkingAction");
                if (ConfigurationStatus)
                    return GetResource("ServiceHostWizardRecheckAction");
                if (HasError)
                    return GetResource("ServiceHostWizardRetryAction");
                if (_status?.NeedsInstall == true)
                    return GetResource("ServiceHostWizardInstallAction");
                if (_status?.State == ServiceHostInstallState.Stopped)
                    return GetResource("ServiceHostWizardRepairAction");
                if (_status?.NeedsUpdate == true || _status?.NeedsRepair == true)
                    return GetResource("ServiceHostWizardUpdateAction");

                return GetResource("ServiceHostWizardRetryAction");
            }
        }

        public override string Description
        {
            get
            {
                if (IsBusy)
                    return GetResource("ServiceHostWizardWorking");
                if (_status == null)
                    return GetResource("ServiceHostWizardChecking");
                if (ColorVisionServiceHostManager.IsReadyForPackagedVersion(_status))
                {
                    return string.Format(
                        GetResource("ServiceHostWizardReadyFormat"),
                        _status.RunningVersion?.ToString() ?? "-");
                }
                if (!_status.IsPackageAvailable || _status.PackageVersion == null)
                {
                    return string.Format(
                        GetResource("ServiceHostWizardUnavailableFormat"),
                        _status.PackageExecutablePath);
                }
                if (_status.NeedsInstall)
                {
                    return string.Format(
                        GetResource("ServiceHostWizardNotInstalledFormat"),
                        _status.PackageExecutablePath);
                }
                if (_status.State == ServiceHostInstallState.Stopped)
                {
                    return string.Format(
                        GetResource("ServiceHostWizardStoppedFormat"),
                        _status.InstalledVersion?.ToString() ?? "-");
                }
                if (_status.NeedsUpdate || _status.NeedsRepair)
                {
                    return string.Format(
                        GetResource("ServiceHostWizardUpdateFormat"),
                        _status.InstalledVersion?.ToString() ?? "-",
                        _status.PackageVersion?.ToString() ?? "-");
                }

                return GetResource("ServiceHostWizardChecking");
            }
        }

        public override Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            return RefreshStatusAsync(cancellationToken);
        }

        private async Task ExecuteAsync()
        {
            if (IsBusy)
                return;

            if (ConfigurationStatus)
            {
                await RefreshStatusAsync().ConfigureAwait(true);
                return;
            }

            SetBusy(true);
            _errorMessage = string.Empty;
            NotifyStateChanged();
            try
            {
                ServiceHostEnsureResult result = await ColorVisionServiceHostManager
                    .EnsureReadyAsync()
                    .ConfigureAwait(true);
                _status = result.Status;
                _errorMessage = result.Success ? string.Empty : result.Error;
            }
            catch (Exception ex)
            {
                _errorMessage = ex.Message;
            }
            finally
            {
                SetBusy(false);
                NotifyStateChanged();
            }
        }

        private async Task RefreshStatusAsync(CancellationToken cancellationToken = default)
        {
            if (IsBusy)
                return;

            SetBusy(true);
            _errorMessage = string.Empty;
            NotifyStateChanged();
            try
            {
                _status = await ColorVisionServiceHostManager
                    .QueryStatusAsync(cancellationToken)
                    .ConfigureAwait(true);

                if (!_status.IsReady && (!_status.IsPackageAvailable || _status.PackageVersion == null))
                {
                    _errorMessage = string.Format(
                        GetResource("ServiceHostWizardPackageMissingFormat"),
                        _status.PackageExecutablePath);
                }
                else if (_status.State == ServiceHostInstallState.Unknown)
                {
                    _errorMessage = string.IsNullOrWhiteSpace(_status.RawOutput)
                        ? GetResource("ServiceHostWizardStatusUnknown")
                        : _status.RawOutput;
                }
            }
            catch (Exception ex)
            {
                _errorMessage = ex.Message;
            }
            finally
            {
                SetBusy(false);
                NotifyStateChanged();
            }
        }

        private void SetBusy(bool value)
        {
            if (_isBusy == value)
                return;

            _isBusy = value;
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(CanContinue));
            CommandManager.InvalidateRequerySuggested();
        }

        private void NotifyStateChanged()
        {
            OnPropertyChanged(nameof(ConfigurationStatus));
            OnPropertyChanged(nameof(CanContinue));
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(ErrorMessage));
            OnPropertyChanged(nameof(ActionText));
            OnPropertyChanged(nameof(Description));
        }

        private static string GetResource(string name)
        {
            return Properties.Resources.ResourceManager.GetString(name, Properties.Resources.Culture) ?? name;
        }
    }
}
