using ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public class CameraRunParamTests
{
    [Fact]
    public void SetAllExposure_UpdatesEveryExposureField()
    {
        var param = new CameraRunParam
        {
            ExpTime = 1,
            ExpTimeR = 2,
            ExpTimeG = 3,
            ExpTimeB = 4
        };

        param.SetAllExposure(80);

        Assert.Equal(80f, param.ExpTime);
        Assert.Equal(80f, param.ExpTimeR);
        Assert.Equal(80f, param.ExpTimeG);
        Assert.Equal(80f, param.ExpTimeB);
    }

    [Fact]
    public void CustomEditor_AppliesOneValueToEveryExposureField()
    {
        StaTest.Run(() =>
        {
            var param = new CameraRunParam
            {
                ExpTime = 1,
                ExpTimeR = 2,
                ExpTimeG = 3,
                ExpTimeB = 4
            };
            var editor = new CameraRunParamEditor();
            editor.SetParam(param);

            var textBox = Assert.IsAssignableFrom<TextBox>(editor.FindName("UnifiedExposureTextBox"));
            var applyButton = Assert.IsAssignableFrom<Button>(editor.FindName("ApplyUnifiedExposureButton"));
            textBox.Text = "80";
            applyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal(80f, param.ExpTime);
            Assert.Equal(80f, param.ExpTimeR);
            Assert.Equal(80f, param.ExpTimeG);
            Assert.Equal(80f, param.ExpTimeB);
        });
    }
}
