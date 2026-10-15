using ColorVision.Engine;
using ColorVision.ImageEditor;
using ColorVision.Themes;
using System.Reflection;
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
            List<ImageView> previews = [image, algorithm];
            try
            {
                frame.Navigate(page);
                window.Show();
                PumpDispatcher();
                Assert.Same(page, frame.Content);
                SetPreviewImages(image, algorithm);

                if (navigateBeforeClose)
                {
                    var nextPage = CreateOfflinePage(frame);
                    var nextImage = Preview(nextPage, "imagePreview");
                    var nextAlgorithm = Preview(nextPage, "algorithmImagePreview");
                    previews.AddRange([nextImage, nextAlgorithm]);
                    frame.Navigate(nextPage);
                    PumpDispatcher();
                    Assert.Same(nextPage, frame.Content);
                    SetPreviewImages(nextImage, nextAlgorithm);
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
                    Assert.Same(nextPage, frame.Content);
                    SetPreviewImages(nextImage, nextAlgorithm);
                }

                window.Close();
                PumpDispatcher();
                Assert.All(previews, preview =>
                {
                    Assert.Null(preview.ViewBitmapSource);
                    Assert.Throws<ObjectDisposedException>(() => preview.RegisterSettingsProvider(() => []));
                });
            }
            finally
            {
                window.Close();
                foreach (ImageView preview in previews) preview.Dispose();
                PumpDispatcher();
                Application.Current.MainWindow = previousMainWindow;
            }
        });
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
