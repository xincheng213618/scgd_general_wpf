using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ColorVision.UI;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        new Application();
        var canvas = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (bool dark in new[] { false, true })
        {
            var rows = new StackPanel { Width = 300, Margin = new Thickness(16) };
            var surface = new Border { Background = dark ? new SolidColorBrush(Color.FromRgb(32, 35, 39)) : Brushes.White, Child = rows };
            surface.Resources["GlobalTextBrush"] = dark ? Brushes.WhiteSmoke : Brushes.Black;
            surface.Resources["GlobalBorderBrush"] = dark ? Brushes.DimGray : Brushes.LightGray;
            rows.Children.Add(new TextBlock { Text = dark ? "深色 · 设备控制" : "浅色 · 设备控制", Foreground = (Brush)surface.Resources["GlobalTextBrush"], FontSize = 16, Margin = new Thickness(0, 0, 0, 12) });
            foreach (var (name, pinned) in new[] { ("SV6100_Camera", true), ("工作流程", false), ("SV6100_Algorithm", false) })
            {
                var header = new DockPanel { Height = 30 };
                var settings = new TextBlock { Text = "ⓘ", Width = 18, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.ForestGreen, FontSize = 16 };
                DockPanel.SetDock(settings, Dock.Right);
                header.Children.Add(settings);
                var pin = new DisplayPinButton { IsChecked = pinned };
                if (!pinned && name == "工作流程") pin.Opacity = 0.75;
                DockPanel.SetDock(pin, Dock.Right);
                header.Children.Add(pin);
                header.Children.Add(new TextBlock { Text = "⌄  " + name, FontSize = 15, Foreground = (Brush)surface.Resources["GlobalTextBrush"], VerticalAlignment = VerticalAlignment.Center });
                var card = new StackPanel();
                card.Children.Add(header);
                card.Children.Add(new Border { Background = dark ? new SolidColorBrush(Color.FromRgb(42, 45, 49)) : Brushes.WhiteSmoke, BorderBrush = dark ? Brushes.DimGray : Brushes.LightGray, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(8), Child = new TextBlock { Text = name == "工作流程" ? "White255_Fast_Test       执行流程" : "连接", FontSize = 13, Foreground = (Brush)surface.Resources["GlobalTextBrush"] } });
                rows.Children.Add(new UserControl { Content = card, Margin = new Thickness(0, 0, 0, 6) });
            }
            canvas.Children.Add(surface);
        }
        canvas.Measure(new Size(664, 340));
        canvas.Arrange(new Rect(0, 0, 664, 340));
        canvas.UpdateLayout();
        var bitmap = new RenderTargetBitmap(996, 510, 144, 144, PixelFormats.Pbgra32);
        bitmap.Render(canvas);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(AppContext.BaseDirectory, "display-pin-preview.png"));
        png.Save(stream);
    }
}
