using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Desktop.Wizards;
using ColorVision.Update;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ColorVision.Wizards
{
    public sealed class ApplicationUpdateWizardStep : WizardStepBase
    {
        private readonly Func<CancellationToken, Task<AutoUpdatePlanCheckResult>> _checkUpdate;
        private readonly Action _prepareWizardResume;
        private readonly Action<AutoUpdatePlan, Action> _startUpdate;
        private readonly RelayCommand _command;
        private AutoUpdatePlan? _plan;
        private bool _isChecking;
        private bool _isUpdating;
        private bool _reviewCompleted;
        private string _status = string.Empty;
        private string _errorMessage = string.Empty;

        public ApplicationUpdateWizardStep() : this(
            token => AutoUpdater.GetUpdatePlanCheckResultAsync(forceRefresh: true, token),
            () => PrepareWizardResume(WizardWindowConfig.Instance, () => ConfigHandler.GetInstance().SaveConfigs()),
            (plan, failed) => AutoUpdater.StartUpdatePlan(plan, failed)) { }

        internal ApplicationUpdateWizardStep(
            Func<CancellationToken, Task<AutoUpdatePlanCheckResult>> checkUpdate,
            Action prepareWizardResume,
            Action<AutoUpdatePlan, Action> startUpdate)
        {
            _checkUpdate = checkUpdate;
            _prepareWizardResume = prepareWizardResume;
            _startUpdate = startUpdate;
            _command = new RelayCommand(_ => _ = ExecuteAsync(), _ => !IsBusy);
        }

        public override int Order => -1100;
        public override string Header => GetResource("ApplicationUpdateWizardHeader");
        public override bool RunsBeforeInitializers => true;
        public override bool IsBusy => _isChecking || _isUpdating;
        public override bool HasError => !string.IsNullOrEmpty(_errorMessage);
        public override string ErrorMessage => _errorMessage;
        public override ICommand Command => _command;
        public override bool ConfigurationStatus { get => _reviewCompleted; set { } }
        public override string Description => string.Format(GetResource("ApplicationUpdateWizardIntroFormat"), AutoUpdater.CurrentVersion)
            + Environment.NewLine + Environment.NewLine
            + (_isUpdating ? GetResource("ApplicationUpdateWizardUpdating")
                : _isChecking ? GetResource("ApplicationUpdateWizardChecking") : _status);
        public override string ActionText => _plan == null
            ? GetResource("ApplicationUpdateWizardRecheckAction")
            : string.Format(GetResource("ApplicationUpdateWizardUpdateActionFormat"), _plan.TargetVersion);

        public override async Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            if (IsBusy) return;
            _isChecking = true;
            _plan = null;
            _status = string.Empty;
            _errorMessage = string.Empty;
            NotifyStateChanged();
            try
            {
                AutoUpdatePlanCheckResult result = await _checkUpdate(cancellationToken);
                // Offline/error results may contain cached metadata; never present that as a successful check.
                if (result.Status != UpdateServerCheckStatus.Success)
                {
                    _errorMessage = GetResource(result.Status == UpdateServerCheckStatus.NoInternetConnection
                        ? "ApplicationUpdateWizardOffline" : "ApplicationUpdateWizardUnavailable");
                    return;
                }
                _plan = result.Plan;
                _status = _plan == null ? GetResource("ApplicationUpdateWizardNoUpdate")
                    : string.Format(GetResource("ApplicationUpdateWizardAvailableFormat"), _plan.TargetVersion);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception ex) { _errorMessage = ex.GetBaseException().Message; }
            finally { _isChecking = false; NotifyStateChanged(); }
        }

        public override Task<bool> ApplyAsync(CancellationToken cancellationToken = default)
        {
            if (IsBusy) return Task.FromResult(false);
            // Next always skips the optional update, including when a newer version is available.
            _reviewCompleted = true;
            NotifyStateChanged();
            return Task.FromResult(true);
        }

        internal async Task ExecuteAsync()
        {
            if (IsBusy) return;
            if (_plan == null)
            {
                await RefreshAsync();
                return;
            }

            _isUpdating = true;
            _errorMessage = string.Empty;
            NotifyStateChanged();
            try
            {
                // Persist before handing off to the updater, even when reopened from a completed setup.
                _prepareWizardResume();
                _startUpdate(_plan, OnUpdateFailed);
            }
            catch (Exception ex)
            {
                _isUpdating = false;
                _errorMessage = ex.GetBaseException().Message;
                NotifyStateChanged();
            }
        }

        internal static void PrepareWizardResume(WizardWindowConfig config, Action saveConfigs)
        {
            bool wasComplete = config.WizardCompletionKey;
            config.WizardCompletionKey = false;
            try { saveConfigs(); }
            catch
            {
                config.WizardCompletionKey = wasComplete;
                throw;
            }
        }

        private void OnUpdateFailed()
        {
            _isUpdating = false;
            _errorMessage = GetResource("ApplicationUpdateWizardFailed");
            NotifyStateChanged();
        }

        private void NotifyStateChanged()
        {
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(CanContinue));
            OnPropertyChanged(nameof(ConfigurationStatus));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(ActionText));
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(ErrorMessage));
            CommandManager.InvalidateRequerySuggested();
        }

        private static string GetResource(string name) => Properties.Resources.ResourceManager.GetString(name, Properties.Resources.Culture) ?? name;
    }
}
