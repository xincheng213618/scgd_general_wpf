using ColorVision.Themes;
using ColorVision.UI;
using Microsoft.Win32;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Update
{
    public partial class UpdatePreviewWindow
    {
        private readonly Func<UpdatePreviewWindow, Task>? _initializeAsync;
        private bool _hasInitialized;

        public UpdatePreviewAction ResultAction { get; private set; } = UpdatePreviewAction.None;

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

        private void CopyOfflineDownloadCommandButton_Click(object sender, RoutedEventArgs e)
        {
            ColorVision.Common.Clipboard.SetText(AutoUpdater.GetOfflineInstallerDownloadPowerShellCommand());
            CopyOfflineDownloadCommandButton.Content = Properties.Resources.UpdatePreviewOfflineDownloadCommandCopied;
            CopyOfflineDownloadCommandButton.ToolTip = Properties.Resources.UpdatePreviewOfflineDownloadCommandCopiedDescription;
        }

        private void CopyOfflineDownloadCommandButton_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            SaveFileDialog dialog = new()
            {
                AddExtension = true,
                DefaultExt = ".ps1",
                FileName = "Download-ColorVision.ps1",
                Filter = "PowerShell 脚本 (*.ps1)|*.ps1|所有文件 (*.*)|*.*",
                OverwritePrompt = true,
                Title = Properties.Resources.UpdatePreviewSaveOfflineDownloadCommand,
            };
            if (dialog.ShowDialog(this) != true)
                return;

            try
            {
                File.WriteAllText(
                    dialog.FileName,
                    AutoUpdater.GetOfflineInstallerDownloadPowerShellCommand(),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                CopyOfflineDownloadCommandButton.Content = Properties.Resources.UpdatePreviewOfflineDownloadCommandSaved;
                CopyOfflineDownloadCommandButton.ToolTip = string.Format(
                    Properties.Resources.UpdatePreviewOfflineDownloadCommandSavedDescriptionFormat,
                    dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
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
