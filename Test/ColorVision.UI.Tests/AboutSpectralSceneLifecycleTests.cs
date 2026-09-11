using System.Runtime.CompilerServices;
using ColorVision.UI.Views.About;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class AboutSpectralSceneLifecycleTests
{
    [Theory]
    [InlineData(AboutArtwork.Spectrum, true)]
    [InlineData(AboutArtwork.Spectrum, false)]
    [InlineData(AboutArtwork.Vision, true)]
    [InlineData(AboutArtwork.Vision, false)]
    public void ClosingTheWindowReleasesTheSceneAfterVisibilityAndMotionChanges(AboutArtwork artwork, bool dark)
    {
        WeakReference reference = WpfTestHost.Invoke(() => CreateClosedScene(artwork, dark));
        for (int i = 0; i < 3 && reference.IsAlive; i++)
        {
            WpfTestHost.Invoke(PumpDispatcher);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        Assert.False(reference.IsAlive, "The closed exhibition must not be retained by Rendering, system settings, or render-tier events.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateClosedScene(AboutArtwork artwork, bool dark)
    {
        Window? previousMainWindow = Application.Current.MainWindow;
        var scene = new AboutArtScene { Artwork = artwork, IsDark = dark, Width = 880, Height = 590 };
        var window = new Window
        {
            Content = scene, Width = 880, Height = 590, ShowActivated = false, ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000
        };
        try
        {
            window.Show();
            PumpDispatcher();
            Assert.True(scene.IsLoaded);
            Assert.NotNull(PresentationSource.FromVisual(scene));
            scene.MotionEnabled = false;
            window.Hide();
            PumpDispatcher();
            window.Show();
            scene.IsDark = !dark;
            scene.MotionEnabled = true;
            PumpDispatcher();
        }
        finally
        {
            window.Close();
            PumpDispatcher();
            Application.Current.MainWindow = previousMainWindow;
        }
        Assert.False(scene.IsLoaded);
        return new WeakReference(scene);
    }

    private static void PumpDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
