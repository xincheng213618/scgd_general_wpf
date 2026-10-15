using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.UI;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public class MotorConfigurationEditorTests
{
    [Fact]
    public void MotorAndVidSwitchesLeadTheirEditorsAndRevealDetailsWithoutRebuilding() => WpfTestHost.Invoke(() =>
    {
        var resources = Application.Current.Resources;
        var replacements = new Dictionary<string, object>
        {
            ["GlobalTextBrush"] = Brushes.Black, ["GlobalBorderBrush"] = Brushes.LightGray, ["BorderBrush"] = Brushes.Gray,
            ["ButtonCommand"] = new Style(typeof(Button)), ["ComboBox.Small"] = new Style(typeof(ComboBox)),
            ["TextBox.Small"] = new Style(typeof(TextBox)), ["bool2VisibilityConverter"] = new BooleanToVisibilityConverter(),
            ["ComboBoxPlus.Small"] = new Style(typeof(HandyControl.Controls.ComboBox)), ["DrawingImageEdit"] = new DrawingImage()
        };
        var previous = replacements.Keys.ToDictionary(key => key, key => resources[key]);
        foreach (var item in replacements) resources[item.Key] = item.Value;
        try
        {
            var config = new MotorConfig();
            Assert.False(config.IsCameraLinkage);
            var editor = PropertyEditorHelper.GenPropertyEditorControl(config);
            var rows = Descendants(editor).OfType<DockPanel>().Where(row => row.Tag is PropertyInfo).ToArray();
            Assert.DoesNotContain(rows, row => ((PropertyInfo)row.Tag).Name == nameof(MotorConfig.IsCameraLinkage));
            var motorSwitch = rows.First(row => ((PropertyInfo)row.Tag).Name == nameof(MotorConfig.IsUseMotor));
            Assert.Same(motorSwitch, rows.First());
            Assert.All(rows.Where(row => row != motorSwitch), row => Assert.False(VisibleWithin(row, editor)));
            Render(editor, "motor-disabled.png");

            config.IsUseMotor = true;
            Assert.False(config.IsCameraLinkage);
            var speed = rows.Single(row => ((PropertyInfo)row.Tag).Name == nameof(MotorConfig.RunSpeed));
            Assert.True(VisibleWithin(speed, editor));
            var vidSwitch = rows.Single(row => ((PropertyInfo)row.Tag).Name == nameof(MotorVID.IsUseVID));
            var mapping = rows.Single(row => ((PropertyInfo)row.Tag).Name == nameof(MotorVID.MappingFileName));
            Assert.True(VisibleWithin(vidSwitch, editor));
            Assert.False(VisibleWithin(mapping, editor));
            config.VID.IsUseVID = true;
            Assert.True(VisibleWithin(mapping, editor));
            Render(editor, "motor-enabled.png");
            config.VID.IsUseVID = false;
            Assert.False(VisibleWithin(mapping, editor));
            config.IsUseMotor = false;
            Assert.False(VisibleWithin(speed, editor));
            Assert.False(VisibleWithin(vidSwitch, editor));

            // Independent motor devices use the base settings without an enable switch.
            var standalone = PropertyEditorHelper.GenPropertyEditorControl(new MotorConfigBase());
            Assert.All(Descendants(standalone).OfType<DockPanel>().Where(row => row.Tag is PropertyInfo), row => Assert.True(VisibleWithin(row, standalone)));
        }
        finally
        {
            foreach (var item in previous)
            {
                if (item.Value != null) resources[item.Key] = item.Value;
                else resources.Remove(item.Key);
            }
        }
    });

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is not DependencyObject dependency) continue;
            yield return dependency;
            foreach (var nested in Descendants(dependency)) yield return nested;
        }
    }

    private static bool VisibleWithin(FrameworkElement row, FrameworkElement root)
    {
        for (DependencyObject? current = row; current != null; current = LogicalTreeHelper.GetParent(current))
        {
            if (current is FrameworkElement element && element.Visibility != Visibility.Visible) return false;
            if (ReferenceEquals(current, root)) return true;
        }
        return false;
    }

    private static void Render(FrameworkElement editor, string name)
    {
        var canvas = new Border { Background = Brushes.White, Padding = new Thickness(16), Child = editor, Width = 820 };
        canvas.Measure(new Size(820, double.PositiveInfinity));
        canvas.Arrange(new Rect(new Size(820, canvas.DesiredSize.Height)));
        canvas.UpdateLayout();
        var bitmap = new RenderTargetBitmap(820, Math.Max(1, (int)Math.Ceiling(canvas.ActualHeight)), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(canvas);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        string directory = Path.Combine(Path.GetTempPath(), "ColorVisionLocalMotorVerification");
        Directory.CreateDirectory(directory);
        using var stream = File.Create(Path.Combine(directory, name));
        encoder.Save(stream);
        canvas.Child = null;
    }
}
