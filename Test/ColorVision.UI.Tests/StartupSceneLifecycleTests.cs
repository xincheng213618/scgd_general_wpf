using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class StartupSceneLifecycleTests
{
    [Fact]
    public void ShownSceneAndReplacedPaletteLayersCanBeCollectedAfterItsWindowCloses()
    {
        (WeakReference sceneReference, WeakReference retiredLayerReference) = WpfTestHost.Invoke(CreateClosedSceneReference);

        for (int i = 0; i < 3 && (sceneReference.IsAlive || retiredLayerReference.IsAlive); i++)
        {
            WpfTestHost.Invoke(PumpDispatcher);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Assert.False(sceneReference.IsAlive, "Closing the window must release the loaded startup scene and its event/animation subscriptions.");
        Assert.False(retiredLayerReference.IsAlive, "A palette change must release replaced drawing layers and their bitmap caches.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Scene, WeakReference RetiredLayer) CreateClosedSceneReference()
    {
        Window? previousMainWindow = Application.Current.MainWindow;
        var scene = new StartupScene { Width = 820, Height = 460, IsDark = false };
        WeakReference retiredLayerReference = null!;
        bool loaded = false;
        bool unloaded = false;
        scene.Loaded += (_, _) => loaded = true;
        scene.Unloaded += (_, _) => unloaded = true;
        var window = new Window
        {
            Content = scene,
            Width = 820,
            Height = 460,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -10000,
            Top = -10000,
        };
        try
        {
            window.Show();
            PumpDispatcher();

            Assert.True(loaded);
            Assert.True(scene.IsLoaded);
            Assert.True(scene.IsVisible);
            Assert.NotNull(PresentationSource.FromVisual(scene));
            AssertPalette(scene, false);

            retiredLayerReference = ReplacePalette(scene, true);
            AssertPalette(scene, true);
            scene.IsDark = false;
            scene.UpdateLayout();
            PumpDispatcher();
            AssertPalette(scene, false);
            window.Hide();
            PumpDispatcher();
            window.Show();
            PumpDispatcher();
            AssertPalette(scene, false);
        }
        finally
        {
            window.Close();
            PumpDispatcher();
            Application.Current.MainWindow = previousMainWindow;
        }

        Assert.True(unloaded);
        Assert.False(scene.IsLoaded);
        return (new WeakReference(scene), retiredLayerReference);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference ReplacePalette(StartupScene scene, bool dark)
    {
        var previousLayer = VisualTreeHelper.GetChild(scene, SystemParameters.HighContrast ? 0 : 2);
        var reference = new WeakReference(previousLayer);
        scene.IsDark = dark;
        scene.UpdateLayout();
        PumpDispatcher();
        Assert.Null(VisualTreeHelper.GetParent(previousLayer));
        return reference;
    }

    private static void AssertPalette(StartupScene scene, bool dark)
    {
        Assert.Equal(dark, scene.IsDark);
        Assert.Equal(SystemParameters.HighContrast ? 1 : 5, VisualTreeHelper.GetChildrenCount(scene));
        Color expectedSurface = SystemParameters.HighContrast ? SystemColors.WindowColor
            : dark ? Color.FromRgb(8, 14, 25) : Color.FromRgb(244, 245, 247);
        var bitmap = new RenderTargetBitmap(820, 460, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(scene);
        foreach (int y in new[] { 4, 455 })
        {
            var pixel = new byte[4];
            bitmap.CopyPixels(new Int32Rect(4, y, 1, 1), pixel, 4, 0);
            Assert.Equal(expectedSurface, Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]));
        }
    }

    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
