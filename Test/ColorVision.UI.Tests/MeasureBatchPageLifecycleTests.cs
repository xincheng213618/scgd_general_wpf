using ColorVision.Engine;
using ColorVision.ImageEditor;
using ColorVision.Themes;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class MeasureBatchPageLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClosingHostDisposesCurrentAndJournaledPreviews(bool navigateBeforeClose)
    {
        WpfTestHost.Invoke(() =>
        {
            using var resources = new PresentationResources();
            Window? previousMainWindow = Application.Current.MainWindow;
            var frame = new Frame();
            var window = CreateWindow(frame);
            var page = CreateOfflinePage(frame);
            var image = Preview(page, "imagePreview");
            var algorithm = Preview(page, "algorithmImagePreview");
            try
            {
                frame.Navigate(page);
                window.Show();
                PumpDispatcher();
                Assert.Same(page, frame.Content);
                SetPreviewImages(image, algorithm);

                if (navigateBeforeClose)
                {
                    frame.Navigate(new Page());
                    PumpDispatcher();
                    Assert.Null(image.ViewBitmapSource);
                    Assert.Null(algorithm.ViewBitmapSource);
                    using (image.RegisterSettingsProvider(() => [])) { }
                    using (algorithm.RegisterSettingsProvider(() => [])) { }

                    frame.GoBack();
                    PumpDispatcher();
                    Assert.Same(page, frame.Content);
                    SetPreviewImages(image, algorithm);
                    frame.GoForward();
                    PumpDispatcher();
                }

                window.Close();
                PumpDispatcher();
                Assert.Null(image.ViewBitmapSource);
                Assert.Null(algorithm.ViewBitmapSource);
                Assert.Throws<ObjectDisposedException>(() => image.RegisterSettingsProvider(() => []));
                Assert.Throws<ObjectDisposedException>(() => algorithm.RegisterSettingsProvider(() => []));
            }
            finally
            {
                window.Close();
                image.Dispose();
                algorithm.Dispose();
                PumpDispatcher();
                Application.Current.MainWindow = previousMainWindow;
            }
        });
    }

    [Fact]
    public void RepeatedClosedWindowsAndAllVisitedDetailsCanBeCollected()
    {
        WeakReference[] references = WpfTestHost.Invoke(CreateClosedWindowReferences);
        try
        {
            for (int i = 0; i < 3 && references.Any(reference => reference.IsAlive); i++)
            {
                WpfTestHost.Invoke(PumpDispatcher);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            Assert.All(references, reference => Assert.False(reference.IsAlive,
                "Closed batch-result windows, their navigation history and both previews must be collectible."));
        }
        finally
        {
            // Avoid leaving subscriptions behind when this regression test fails.
            WpfTestHost.Invoke(() =>
            {
                foreach (var reference in references)
                    if (reference.Target is ImageView image) image.Dispose();
            });
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] CreateClosedWindowReferences()
    {
        using var resources = new PresentationResources();
        Window? previousMainWindow = Application.Current.MainWindow;
        var references = new List<WeakReference>();
        try
        {
            for (int i = 0; i < 3; i++)
            {
                var frame = new Frame();
                var window = CreateWindow(frame);
                try
                {
                    window.Show();
                    for (int j = 0; j < 2; j++)
                    {
                        var page = CreateOfflinePage(frame);
                        frame.Navigate(page);
                        PumpDispatcher();
                        Assert.Same(page, frame.Content);
                        var image = Preview(page, "imagePreview");
                        var algorithm = Preview(page, "algorithmImagePreview");
                        SetPreviewImages(image, algorithm);
                        references.Add(new WeakReference(page));
                        references.Add(new WeakReference(image));
                        references.Add(new WeakReference(algorithm));
                    }
                    references.Add(new WeakReference(frame));
                    references.Add(new WeakReference(window));
                }
                finally
                {
                    window.Close();
                    PumpDispatcher();
                }
            }
        }
        finally
        {
            Application.Current.MainWindow = previousMainWindow;
        }
        return references.ToArray();
    }

    private static MeasureBatchPage CreateOfflinePage(Frame frame)
    {
        var page = new MeasureBatchPage(frame, new MeasureBatchModel { Code = "lifecycle-test" });
        // Exercise real WPF navigation/closure without querying the production MySQL database.
        var databaseLoad = typeof(MeasureBatchPage).GetMethod("Page_Loaded", BindingFlags.Instance | BindingFlags.NonPublic)!;
        page.Loaded -= (RoutedEventHandler)databaseLoad.CreateDelegate(typeof(RoutedEventHandler), page);
        return page;
    }

    private static ImageView Preview(MeasureBatchPage page, string name) => Assert.IsType<ImageView>(page.FindName(name));

    private static void SetPreviewImages(ImageView image, ImageView algorithm)
    {
        image.SetImageSource(new WriteableBitmap(64, 32, 96, 96, PixelFormats.Gray8, null));
        algorithm.SetImageSource(new WriteableBitmap(64, 32, 96, 96, PixelFormats.Gray8, null));
        using var imageFrame = image.AcquireImageFrame();
        using var algorithmFrame = algorithm.AcquireImageFrame();
        Assert.NotNull(imageFrame);
        Assert.NotNull(algorithmFrame);
    }

    private static Window CreateWindow(Frame frame) => new()
    {
        Content = frame,
        Width = 900,
        Height = 600,
        ShowActivated = false,
        ShowInTaskbar = false,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Left = -10000,
        Top = -10000,
    };

    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private sealed class PresentationResources : IDisposable
    {
        private readonly List<ResourceDictionary> _dictionaries = [];

        public PresentationResources()
        {
            foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
            {
                var dictionary = new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                _dictionaries.Add(dictionary);
                Application.Current.Resources.MergedDictionaries.Add(dictionary);
            }
        }

        public void Dispose()
        {
            foreach (var dictionary in _dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
        }
    }
}
