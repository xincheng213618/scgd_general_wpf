using ColorVision.Common.MVVM;
using ColorVision.Common.ThirdPartyApps;
using ColorVision.ToolPlugins.ThirdPartyApps;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ColorVision.Wizards
{
    internal interface IRecommendedSoftwareService
    {
        IReadOnlyList<ThirdPartyAppInfo> CreateApps();
        void RefreshStatus(ThirdPartyAppInfo app);
        bool InstallerExists(ThirdPartyAppInfo app);
        Task InstallAsync(ThirdPartyAppInfo app, CancellationToken cancellationToken);
    }

    internal sealed class BundledRecommendedSoftwareService : IRecommendedSoftwareService
    {
        public IReadOnlyList<ThirdPartyAppInfo> CreateApps()
        {
            return new KnownAppProvider()
                .GetThirdPartyApps()
                .Where(app => app.Name is "Everything" or "WinRAR")
                .OrderBy(app => app.Order)
                .ToList();
        }

        public void RefreshStatus(ThirdPartyAppInfo app)
        {
            app.InstallerPath = ResolveInstallerPath(app.InstallerPath);
            app.RefreshStatus();
        }

        public bool InstallerExists(ThirdPartyAppInfo app)
        {
            app.InstallerPath = ResolveInstallerPath(app.InstallerPath);
            return File.Exists(app.InstallerPath);
        }

        public async Task InstallAsync(ThirdPartyAppInfo app, CancellationToken cancellationToken)
        {
            app.InstallerPath = ResolveInstallerPath(app.InstallerPath);
            using Process? process = Process.Start(new ProcessStartInfo(app.InstallerPath)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(app.InstallerPath)
            });
            if (process == null)
                throw new InvalidOperationException($"Unable to start {app.Name} installer.");

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(true);

            for (int attempt = 0; attempt < 10; attempt++)
            {
                app.RefreshStatus();
                if (app.IsInstalled)
                    return;

                await Task.Delay(300, cancellationToken).ConfigureAwait(true);
            }
        }

        private static string ResolveInstallerPath(string installerPath)
        {
            if (string.IsNullOrWhiteSpace(installerPath) || Path.IsPathRooted(installerPath))
                return installerPath;

            return Path.GetFullPath(installerPath, AppContext.BaseDirectory);
        }
    }

    public sealed class RecommendedSoftwareWizardStep : WizardStepBase
    {
        private readonly IRecommendedSoftwareService _service;
        private readonly List<RecommendedSoftwareChoice> _choices;
        private readonly RelayCommand _command;
        private bool _selectionInitialized;
        private bool _reviewCompleted;
        private bool _isBusy;
        private string _errorMessage = string.Empty;

        public RecommendedSoftwareWizardStep()
            : this(new BundledRecommendedSoftwareService())
        {
        }

        internal RecommendedSoftwareWizardStep(IRecommendedSoftwareService service)
        {
            _service = service;
            _choices = service
                .CreateApps()
                .Select(app => new RecommendedSoftwareChoice(app, SelectionChanged))
                .ToList();
            _command = new RelayCommand(_ => _ = ExecuteAsync(), _ => !IsBusy);
        }

        public override int Order => -900;
        public override string Header => GetResource("RecommendedSoftwareWizardHeader");
        public override string Description => GetResource("RecommendedSoftwareWizardDescription");
        public override IReadOnlyList<IWizardChoice> Choices => _choices;
        public override bool RunsBeforeInitializers => true;
        public override bool IsBusy => _isBusy;
        public override bool HasError => !string.IsNullOrWhiteSpace(_errorMessage);
        public override string ErrorMessage => _errorMessage;
        public override bool CanContinue => !IsBusy;
        public override ICommand? Command => _command;

        public override bool ConfigurationStatus
        {
            get => _reviewCompleted || _choices.All(choice => choice.IsInstalled);
            set { }
        }

        public override string ActionText
        {
            get
            {
                if (IsBusy)
                    return GetResource("RecommendedSoftwareWizardWorkingAction");
                if (_choices.Any(choice => choice.IsSelected && !choice.IsInstalled))
                    return GetResource("RecommendedSoftwareWizardInstallAction");
                if (ConfigurationStatus)
                    return GetResource("RecommendedSoftwareWizardRecheckAction");

                return GetResource("RecommendedSoftwareWizardConfirmAction");
            }
        }

        public override Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            _errorMessage = string.Empty;
            RefreshStatus();
            return Task.CompletedTask;
        }

        public override Task<bool> ApplyAsync(CancellationToken cancellationToken = default)
        {
            return InstallSelectedAsync(cancellationToken);
        }

        private async Task ExecuteAsync()
        {
            await InstallSelectedAsync(CancellationToken.None).ConfigureAwait(true);
        }

        private async Task<bool> InstallSelectedAsync(CancellationToken cancellationToken)
        {
            if (IsBusy)
                return false;

            SetBusy(true);
            _errorMessage = string.Empty;
            NotifyStateChanged();
            try
            {
                RefreshStatus();
                List<RecommendedSoftwareChoice> selectedChoices = _choices
                    .Where(choice => choice.IsSelected && !choice.IsInstalled)
                    .ToList();

                foreach (RecommendedSoftwareChoice choice in selectedChoices)
                {
                    if (!_service.InstallerExists(choice.App))
                    {
                        _errorMessage = string.Format(
                            GetResource("RecommendedSoftwareWizardPackageMissingFormat"),
                            choice.Header,
                            choice.App.InstallerPath);
                        NotifyStateChanged();
                        return false;
                    }

                    try
                    {
                        await _service.InstallAsync(choice.App, cancellationToken).ConfigureAwait(true);
                    }
                    catch (Exception ex)
                    {
                        _errorMessage = string.Format(
                            GetResource("RecommendedSoftwareWizardInstallErrorFormat"),
                            choice.Header,
                            ex.Message);
                        NotifyStateChanged();
                        return false;
                    }

                    _service.RefreshStatus(choice.App);
                    choice.Refresh(false, _service.InstallerExists(choice.App));
                    if (!choice.IsInstalled)
                    {
                        _errorMessage = string.Format(
                            GetResource("RecommendedSoftwareWizardInstallIncompleteFormat"),
                            choice.Header);
                        NotifyStateChanged();
                        return false;
                    }
                }

                _reviewCompleted = true;
                RefreshStatus();
                return true;
            }
            finally
            {
                SetBusy(false);
                NotifyStateChanged();
            }
        }

        private void RefreshStatus()
        {
            foreach (RecommendedSoftwareChoice choice in _choices)
            {
                _service.RefreshStatus(choice.App);
                choice.Refresh(!_selectionInitialized, _service.InstallerExists(choice.App));
            }

            _selectionInitialized = true;
            RecommendedSoftwareChoice? missingPackage = _choices.FirstOrDefault(
                choice => !choice.IsInstalled && !choice.InstallerAvailable);
            if (missingPackage != null)
            {
                _errorMessage = string.Format(
                    GetResource("RecommendedSoftwareWizardPackageMissingFormat"),
                    missingPackage.Header,
                    missingPackage.App.InstallerPath);
            }

            NotifyStateChanged();
        }

        private void SelectionChanged()
        {
            _reviewCompleted = false;
            _errorMessage = string.Empty;
            NotifyStateChanged();
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
        }

        internal static string GetResource(string name)
        {
            return Properties.Resources.ResourceManager.GetString(name, Properties.Resources.Culture) ?? name;
        }
    }

    internal sealed class RecommendedSoftwareChoice : ViewModelBase, IWizardChoice
    {
        private readonly Action _selectionChanged;
        private bool _isSelected;

        public RecommendedSoftwareChoice(ThirdPartyAppInfo app, Action selectionChanged)
        {
            App = app;
            _selectionChanged = selectionChanged;
        }

        internal ThirdPartyAppInfo App { get; }
        internal bool IsInstalled => App.IsInstalled;
        internal bool InstallerAvailable { get; private set; }

        public string Header => App.Name;
        public string Description => App.Name == "Everything"
            ? RecommendedSoftwareWizardStep.GetResource("RecommendedSoftwareEverythingDescription")
            : RecommendedSoftwareWizardStep.GetResource("RecommendedSoftwareWinRarDescription");
        public string StatusText
        {
            get
            {
                if (IsInstalled)
                    return RecommendedSoftwareWizardStep.GetResource("RecommendedSoftwareInstalled");
                if (!InstallerAvailable)
                    return RecommendedSoftwareWizardStep.GetResource("RecommendedSoftwarePackageMissing");

                return IsSelected
                    ? RecommendedSoftwareWizardStep.GetResource("RecommendedSoftwareSelected")
                    : RecommendedSoftwareWizardStep.GetResource("RecommendedSoftwareSkipped");
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value || (value && !IsEnabled))
                    return;

                _isSelected = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
                _selectionChanged();
            }
        }

        public bool IsEnabled => !IsInstalled && InstallerAvailable;

        internal void Refresh(bool initializeSelection, bool installerAvailable)
        {
            InstallerAvailable = installerAvailable;
            if (IsInstalled)
                _isSelected = false;
            else if (initializeSelection)
                _isSelected = installerAvailable;

            OnPropertyChanged(nameof(IsSelected));
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(StatusText));
        }
    }
}
