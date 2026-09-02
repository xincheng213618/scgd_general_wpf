using ColorVision.Themes;
using ColorVision.UI;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Update
{
    public partial class UpdatePreviewWindow
    {
        private readonly Func<UpdatePreviewWindow, Task>? _initializeAsync;
        private bool _hasInitialized;

        public UpdatePreviewAction ResultAction { get; private set; } = UpdatePreviewAction.None;

        internal AutoUpdatePlan? ReinstallPlan { get; private set; }

        public UpdatePreviewDialogContext Context { get; }

        public Task InitializationTask { get; private set; } = Task.CompletedTask;

        public bool IsClosed { get; private set; }

        public bool SuppressPostCheckMessage { get; private set; }

        public UpdatePreviewWindow(UpdatePreviewDialogContext context, Func<UpdatePreviewWindow, Task>? initializeAsync = null)
        {
            Context = context;
            _initializeAsync = initializeAsync;
            DataContext = Context;

            InitializeComponent();
            this.ApplyCaption();

            ContentRendered += UpdatePreviewWindow_ContentRendered;
            Closing += (_, _) =>
            {
                SaveUpdateOptions();
                if (Context.IsChecking)
                {
                    SuppressPostCheckMessage = true;
                }
            };
            Closed += (_, _) =>
            {
                IsClosed = true;
            };
        }

        private async void UpdatePreviewWindow_ContentRendered(object? sender, EventArgs e)
        {
            if (_hasInitialized || _initializeAsync == null)
                return;

            _hasInitialized = true;
            InitializationTask = _initializeAsync(this);

            try
            {
                await InitializationTask;
            }
            catch
            {
                if (!IsClosed)
                {
                    DialogResult = false;
                }
            }
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Context.CanConfirm)
                return;

            SaveUpdateOptions();
            ResultAction = UpdatePreviewAction.UpdateNow;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Context.CanCancel)
                return;

            if (Context.IsChecking)
            {
                SuppressPostCheckMessage = true;
            }

            ResultAction = UpdatePreviewAction.None;
            DialogResult = false;
        }

        private void ApplicationSnapshotsLink_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            new ApplicationSnapshotsWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog();
            Context.CreateSnapshotBeforeUpdate = ApplicationSnapshotConfig.Instance.CreateSnapshotBeforeUpdate;
        }

        private async void ReinstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Context.CanReinstall)
                return;

            Context.IsUpdating = true;
            ReinstallButton.Content = Properties.Resources.UpdatePreviewReinstallChecking;
            try
            {
                SaveUpdateOptions();
                LatestVersionCheckResult latestVersion = await AutoUpdater.GetLatestVersionCheckResultAsync(AutoUpdater.UpdateUrl, forceRefresh: true);
                if (IsClosed)
                    return;

                ReinstallPlan = AutoUpdater.BuildReinstallPlan(AutoUpdater.CurrentVersion ?? latestVersion.Version, latestVersion);
                if (ReinstallPlan == null)
                {
                    string message = latestVersion.Status == UpdateServerCheckStatus.NoInternetConnection
                        ? Properties.Resources.UpdatePreviewNoInternetConnectionMessage
                        : Properties.Resources.UpdatePreviewServerUnavailableMessage;
                    MessageBox.Show(this, message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ResultAction = UpdatePreviewAction.Reinstall;
                DialogResult = true;
            }
            catch (Exception ex)
            {
                if (!IsClosed)
                    MessageBox.Show(this, ex.Message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Context.IsUpdating = false;
                ReinstallButton.Content = Properties.Resources.UpdatePreviewReinstall;
            }
        }

        private void DisableSystemProxyForUpdates_Click(object sender, RoutedEventArgs e)
        {
            UpdateNetworkConfig.Instance.DisableSystemProxyForUpdates = Context.DisableSystemProxyForUpdates;
            ConfigService.Instance.SaveConfigs();
        }

        private void SaveUpdateOptions()
        {
            ApplicationSnapshotConfig.Instance.CreateSnapshotBeforeUpdate = Context.CreateSnapshotBeforeUpdate;
            UpdateNetworkConfig.Instance.DisableSystemProxyForUpdates = Context.DisableSystemProxyForUpdates;
            ConfigService.Instance.SaveConfigs();
        }
    }
}
