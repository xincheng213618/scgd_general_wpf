using log4net;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ColorVision.UI.Desktop.Download
{
    // Applies the user's completion settings whether or not the download window is open.
    internal static class DownloadCompletionNotifier
    {
        private static readonly ILog log = LogManager.GetLogger(nameof(DownloadCompletionNotifier));

        public static void Attach(Aria2cDownloadManager manager)
        {
            manager.DownloadCompleted -= OnDownloadCompleted;
            manager.DownloadCompleted += OnDownloadCompleted;
        }

        private static void OnDownloadCompleted(object? sender, DownloadTask task)
        {
            // Programmatic downloads report to their own caller.
            if (task.OnCompletedCallback != null) return;
            Application.Current?.Dispatcher.BeginInvoke(() => Notify(task, DownloadManagerConfig.Instance));
        }

        private static void Notify(DownloadTask task, DownloadManagerConfig config)
        {
            if (task.Status == DownloadStatus.Completed && config.RunFileAfterDownload)
            {
                OpenFile(task.SavePath);
                return;
            }
            if (!config.ShowCompletedNotification) return;

            if (task.Status == DownloadStatus.Completed)
            {
                var result = MessageBox.Show(
                    string.Format(Properties.Resources.DownloadCompletedMessage, task.FileName) + "\n\n" + Properties.Resources.OpenFileQuestion,
                    Properties.Resources.DownloadManager, MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (result == MessageBoxResult.Yes) OpenFile(task.SavePath);
            }
            else if (task.Status == DownloadStatus.Failed)
            {
                MessageBox.Show(string.Format(Properties.Resources.DownloadFailedMessage, task.FileName, task.ErrorMessage),
                    Properties.Resources.DownloadManager, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static void OpenFile(string path)
        {
            if (!File.Exists(path)) return;
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch (Exception ex)
            {
                log.Error($"Unable to open downloaded file: {path}", ex);
                MessageBox.Show(ex.Message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
