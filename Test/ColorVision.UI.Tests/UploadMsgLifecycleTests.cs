using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Themes.Controls.Uploads;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public class UploadMsgLifecycleTests
{
    private static readonly string[] ThemeResourceKeys = ["GlobalBackground", "ListViewItemBaseStyle", "InputElementBaseStyle"];
    private static readonly FieldInfo PhyCameraUploadClosedField = typeof(PhyCamera).GetField(
        nameof(PhyCamera.UploadClosed),
        BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("PhyCamera.UploadClosed backing field was not found.");

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

    [Fact]
    public void PhyCameraManualClose_AllowsLaterCompletionWithoutSubscriber()
    {
        WpfTestHost.Invoke(() =>
        {
            var previousResources = CaptureThemeResources();
            UploadMsg? window = null;

            try
            {
                EnsureThemeResources();
                PhyCamera camera = CreatePhyCameraWithoutRuntimeDependencies();
                window = new UploadMsg(camera);
                window.Show();
                Assert.Equal(1, GetPhyCameraUploadClosedSubscriptionCount(camera));

                window.Close();
                Assert.Equal(0, GetPhyCameraUploadClosedSubscriptionCount(camera));
                Assert.False(window.IsVisible);
                Assert.Null(window.DataContext);

                Assert.Null(Record.Exception(camera.NotifyUploadClosed));
                Assert.Equal(0, GetPhyCameraUploadClosedSubscriptionCount(camera));
                Assert.False(window.IsVisible);
            }
            finally
            {
                if (window?.IsVisible == true)
                    window.Close();

                RestoreThemeResources(previousResources);
            }
        });
    }

    [Fact]
    public async Task PhyCameraSuccessNotification_RunsSubscriberOnUiThread()
    {
        int uiThreadId = WpfTestHost.Invoke(() => Environment.CurrentManagedThreadId);
        PhyCamera camera = CreatePhyCameraWithoutRuntimeDependencies();
        int subscriberThreadId = 0;
        int notificationCount = 0;
        camera.UploadClosed += (_, _) =>
        {
            subscriberThreadId = Environment.CurrentManagedThreadId;
            notificationCount++;
        };

        await Task.Run(camera.NotifyUploadClosed);

        Assert.Equal(1, notificationCount);
        Assert.Equal(uiThreadId, subscriberThreadId);
    }

    [Fact]
    public void PhyCameraFailureNotification_DoesNotReplaceOriginalException()
    {
        WpfTestHost.Invoke(() => { });
        PhyCamera camera = CreatePhyCameraWithoutRuntimeDependencies();
        var originalException = new InvalidOperationException("original upload failure");
        camera.UploadClosed += (_, _) => throw new ApplicationException("completion subscriber failed");

        Exception? observedException = Record.Exception((Action)(() =>
        {
            try
            {
                throw originalException;
            }
            catch
            {
                camera.NotifyUploadClosed();
                throw;
            }
        }));

        Assert.Same(originalException, observedException);
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

    private static PhyCamera CreatePhyCameraWithoutRuntimeDependencies() =>
        (PhyCamera)RuntimeHelpers.GetUninitializedObject(typeof(PhyCamera));

    private static int GetPhyCameraUploadClosedSubscriptionCount(PhyCamera camera) =>
        (PhyCameraUploadClosedField.GetValue(camera) as MulticastDelegate)?.GetInvocationList().Length ?? 0;

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
