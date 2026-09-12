using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Video;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public class VideoLifecycleTests
{
    private static readonly MethodInfo SetupControlsMethod = GetMethod("SetupVideoControls");

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
        "_writeableBitmap",
        "_session"
    ];

    [Theory]
    [InlineData("config")]
    [InlineData("close")]
    [InlineData("deactivate")]
    public void VideoClosePathsReleaseUiAndKeepSharedToolbarItems(string closePath)
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
                SetupControlsMethod.Invoke(videoOpen, [context]);
                object[] videoControls = ControlFieldNames.Select(name => GetField(videoOpen, name)!).ToArray();
                DispatcherTimer timer = Assert.IsType<DispatcherTimer>(GetField(videoOpen, "_mouseIdleTimer"));
                Assert.True(timer.IsEnabled);
                Assert.All(videoControls, control => Assert.True(imageView.ToolBarAl.Items.Contains(control)));

                switch (closePath)
                {
                    case "config": context.Config.ClearProperties(); break;
                    case "close": videoOpen.Close(); break;
                    case "deactivate": videoOpen.OnEditorToolsDeactivated(context); break;
                }

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

    private static MethodInfo GetMethod(string name)
    {
        return typeof(VideoOpen).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(VideoOpen).FullName, name);
    }

    private static object? GetField(VideoOpen videoOpen, string name)
    {
        return GetFieldInfo(name).GetValue(videoOpen);
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
