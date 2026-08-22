using ColorVision.Core;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Video;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public class VideoLifecycleTests
{
    private static readonly MethodInfo SetupControlsMethod = GetMethod("SetupVideoControls");
    private static readonly MethodInfo FrameCallbackMethod = GetMethod("OnFrameReceived");
    private static readonly MethodInfo StatusCallbackMethod = GetMethod("OnStatusChanged");

    private static readonly string[] ControlFieldNames =
    [
        "_playPauseButton",
        "_stopButton",
        "_muteButton",
        "_progressSlider",
        "_timeTextBlock",
        "_frameInfoTextBlock",
        "_speedComboBox",
        "_resizeComboBox",
        "_autoHideCheckBox"
    ];

    private static readonly string[] ReleasedReferenceFieldNames =
    [
        .. ControlFieldNames,
        "_videoToolBar",
        "_mouseIdleTimer",
        "_mediaPlayer",
        "_writeableBitmap",
        "_imageView",
        "_frameCallbackDelegate",
        "_statusCallbackDelegate",
        "_currentFilePath"
    ];

    [Fact]
    public void ClearPropertiesReleasesVideoUiAndKeepsSharedToolbarItems()
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            ImageView imageView = new();
            EditorContext context = imageView.EditorContext;
            VideoOpen videoOpen = new(context);
            TextBlock sharedItem = new() { Text = "Shared" };
            imageView.ToolBarAl.Items.Add(sharedItem);

            try
            {
                SetField(videoOpen, "_imageView", imageView);
                SetupControlsMethod.Invoke(videoOpen, [context]);
                object[] videoControls = ControlFieldNames.Select(name => GetField(videoOpen, name)!).ToArray();
                DispatcherTimer timer = Assert.IsType<DispatcherTimer>(GetField(videoOpen, "_mouseIdleTimer"));
                Assert.True(timer.IsEnabled);
                Assert.All(videoControls, control => Assert.True(imageView.ToolBarAl.Items.Contains(control)));

                context.Config.ClearProperties();

                Assert.False(timer.IsEnabled);
                Assert.True(imageView.ToolBarAl.Items.Contains(sharedItem));
                Assert.All(videoControls, control => Assert.False(imageView.ToolBarAl.Items.Contains(control)));
                Assert.All(ReleasedReferenceFieldNames, name => Assert.Null(GetField(videoOpen, name)));

                ToolBar marker = new();
                SetField(videoOpen, "_videoToolBar", marker);
                context.Config.ClearProperties();
                Assert.Same(marker, GetField(videoOpen, "_videoToolBar"));
                SetField(videoOpen, "_videoToolBar", null);
            }
            finally
            {
                imageView.ToolBarAl.Items.Remove(sharedItem);
                imageView.Dispose();
            }
        });
    }

    [Fact]
    public void StaleStatusAtCallbackEntryDoesNotChangePlaybackState()
    {
        VideoOpen videoOpen = new(null!);
        SetField(videoOpen, "_videoHandle", 41);
        SetField(videoOpen, "_isPlaying", false);

        StatusCallbackMethod.Invoke(videoOpen, [40, 1, IntPtr.Zero]);

        Assert.False(GetField<bool>(videoOpen, "_isPlaying"));
    }

    [Fact]
    public void QueuedStatusForFormerHandleDoesNotChangePlaybackState()
    {
        WpfTestHost.Invoke(() =>
        {
            VideoOpen videoOpen = new(null!);
            SetField(videoOpen, "_videoHandle", 41);
            SetField(videoOpen, "_isPlaying", false);

            StatusCallbackMethod.Invoke(videoOpen, [41, 1, IntPtr.Zero]);
            SetField(videoOpen, "_videoHandle", 42);
            PumpDispatcher();

            Assert.False(GetField<bool>(videoOpen, "_isPlaying"));
        });
    }

    [Fact]
    public void StaleFrameAtCallbackEntryIsDisposed()
    {
        VideoOpen videoOpen = new(null!);
        SetField(videoOpen, "_videoHandle", 41);
        IntPtr pixels = Marshal.AllocCoTaskMem(3);
        object?[] arguments =
        [
            40,
            new HImage
            {
                rows = 1,
                cols = 1,
                channels = 3,
                depth = 8,
                stride = 3,
                pData = pixels
            },
            0,
            1,
            IntPtr.Zero
        ];

        try
        {
            FrameCallbackMethod.Invoke(videoOpen, arguments);
            HImage disposedFrame = Assert.IsType<HImage>(arguments[1]);
            Assert.Equal(IntPtr.Zero, disposedFrame.pData);
            Assert.Equal(0, GetField<int>(videoOpen, "_isProcessingFrame"));
            pixels = IntPtr.Zero;
        }
        finally
        {
            if (pixels != IntPtr.Zero) Marshal.FreeCoTaskMem(pixels);
        }
    }

    [Fact]
    public void QueuedFrameForFormerHandleIsNotRendered()
    {
        WpfTestHost.Invoke(() =>
        {
            VideoOpen videoOpen = new(null!);
            SetField(videoOpen, "_videoHandle", 41);
            IntPtr pixels = Marshal.AllocCoTaskMem(3);
            Marshal.Copy(new byte[] { 10, 20, 30 }, 0, pixels, 3);
            object?[] arguments =
            [
                41,
                new HImage
                {
                    rows = 1,
                    cols = 1,
                    channels = 3,
                    depth = 8,
                    stride = 3,
                    isDispose = true,
                    pData = pixels
                },
                0,
                1,
                IntPtr.Zero
            ];

            try
            {
                FrameCallbackMethod.Invoke(videoOpen, arguments);
                Assert.Equal(1, GetField<int>(videoOpen, "_isProcessingFrame"));
                SetField(videoOpen, "_videoHandle", 42);
                PumpDispatcher();

                Assert.Equal(0, GetField<int>(videoOpen, "_isProcessingFrame"));
                Assert.Null(GetField(videoOpen, "_writeableBitmap"));
            }
            finally
            {
                Marshal.FreeCoTaskMem(pixels);
            }
        });
    }

    private static MethodInfo GetMethod(string name)
    {
        return typeof(VideoOpen).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(VideoOpen).FullName, name);
    }

    private static object? GetField(VideoOpen videoOpen, string name)
    {
        return GetFieldInfo(name).GetValue(videoOpen);
    }

    private static T GetField<T>(VideoOpen videoOpen, string name)
    {
        return Assert.IsType<T>(GetField(videoOpen, name));
    }

    private static void SetField(VideoOpen videoOpen, string name, object? value)
    {
        GetFieldInfo(name).SetValue(videoOpen, value);
    }

    private static FieldInfo GetFieldInfo(string name)
    {
        return typeof(VideoOpen).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(VideoOpen).FullName, name);
    }

    private static void PumpDispatcher()
    {
        DispatcherFrame frame = new();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static void EnsureImageViewTestResources()
    {
        Application application = Application.Current ?? new Application();
        application.Resources["TextBox.Small"] = new Style(typeof(TextBox));
        application.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
        application.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
        application.Resources["ToolBarImage"] = new Style(typeof(Image));
        application.Resources["BaseStyle"] = new Style(typeof(Control));
        application.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
        application.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
    }
}
