using ColorVision.Themes.Controls.Uploads;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public class UploadMsgLifecycleTests
{
    private static readonly string[] ThemeResourceKeys = ["GlobalBackground", "ListViewItemBaseStyle", "InputElementBaseStyle"];

    [Fact]
    public void UploadClosed_ClosesWindowAndReleasesPublisherSubscription()
    {
        WpfTestHost.Invoke(() =>
        {
            var previousResources = CaptureThemeResources();
            UploadMsg? window = null;

            try
            {
                EnsureThemeResources();
                var upload = new TrackingUploadMsg();
                window = new UploadMsg(upload);
                window.Show();
                Assert.Equal(1, upload.SubscriptionCount);

                upload.Complete();

                Assert.False(window.IsVisible);
                Assert.Equal(0, upload.SubscriptionCount);
                Assert.Null(window.DataContext);
            }
            finally
            {
                if (window?.IsVisible == true)
                    window.Close();

                RestoreThemeResources(previousResources);
            }
        });
    }

    private static Dictionary<string, object?> CaptureThemeResources()
    {
        var resources = Application.Current.Resources;
        var previousResources = new Dictionary<string, object?>();
        foreach (string key in ThemeResourceKeys)
        {
            if (resources.Contains(key))
                previousResources[key] = resources[key];
        }

        return previousResources;
    }

    private static void EnsureThemeResources()
    {
        Application.Current.Resources["GlobalBackground"] = Brushes.Transparent;
        Application.Current.Resources["ListViewItemBaseStyle"] = new Style(typeof(ListViewItem));
        Application.Current.Resources["InputElementBaseStyle"] = new Style(typeof(GridViewColumnHeader));
    }

    private static void RestoreThemeResources(Dictionary<string, object?> previousResources)
    {
        var resources = Application.Current.Resources;
        foreach (string key in ThemeResourceKeys)
        {
            if (previousResources.TryGetValue(key, out object? value))
                resources[key] = value;
            else
                resources.Remove(key);
        }
    }

    private sealed class TrackingUploadMsg : IUploadMsg
    {
        private EventHandler? _uploadClosed;

        public string Msg => "done";
        public ObservableCollection<FileUploadInfo> UploadList { get; } = [];
        public int SubscriptionCount { get; private set; }

        public event EventHandler UploadClosed
        {
            add
            {
                _uploadClosed += value;
                SubscriptionCount++;
            }
            remove
            {
                _uploadClosed -= value;
                SubscriptionCount--;
            }
        }

        public void Complete() => _uploadClosed?.Invoke(this, EventArgs.Empty);
    }
}
