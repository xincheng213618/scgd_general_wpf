using ColorVision.Common.MVVM;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace ColorVision.UI.Tests;

public sealed class WindowServicePresentationTests
{
    [Fact]
    public void RenderConfigurationShellWithSyntheticCommands()
    {
        string? output = Environment.GetEnvironmentVariable("COLORVISION_SERVICE_PREVIEW");
        if (string.IsNullOrEmpty(output)) return;
        WpfTestHost.Invoke(() =>
        {
            var saved = Application.Current.Resources;
            try
            {
                foreach (bool dark in new[] { false, true })
                {
                    var resources = new ResourceDictionary();
                    foreach (string path in new[]
                    {
                        $"/HandyControl;component/Themes/basic/colors/{(dark ? "colorsdark" : "colors")}.xaml",
                        "/HandyControl;component/Themes/Theme.xaml",
                        $"/ColorVision.Themes;component/Themes/{(dark ? "Dark" : "White")}.xaml",
                        "/ColorVision.Themes;component/Themes/Base.xaml",
                        "/ColorVision.Themes;component/Themes/GroupBox.xaml",
                        "/ColorVision.Themes;component/Themes/Icons.xaml"
                    }) resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(path, UriKind.Relative) });
                    Application.Current.Resources = resources;
                    var repository = new DirectoryInfo(AppContext.BaseDirectory);
                    while (repository != null && !File.Exists(Path.Combine(repository.FullName, "build.sln"))) repository = repository.Parent;
                    var doc = XDocument.Load(Path.Combine(repository!.FullName, "Engine/ColorVision.Engine/Services/WindowService.xaml"));
                    XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
                    XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
                    var root = doc.Root!;
                    root.Name = p + "Border";
                    foreach (var attr in root.Attributes().ToList())
                    {
                        if (attr.IsNamespaceDeclaration)
                        {
                            if (attr.Value.StartsWith("clr-namespace:")) attr.Value += ";assembly=ColorVision.Engine";
                        }
                        else if (attr.Name != "Background" && attr.Name != "UseLayoutRounding") attr.Remove();
                    }
                    root.Element(p + "Window.Resources")!.Name = p + "Border.Resources";
                    foreach (var attr in root.Descendants().Attributes().Where(a => new[] { "Click", "SelectedItemChanged" }.Contains(a.Name.LocalName)).ToList()) attr.Remove();
                    var shell = (Border)XamlReader.Parse(doc.ToString());
                    TextElementFont(shell);
                    ((TextBlock)shell.FindName("DetailTitle")).Text = "SV6100_Camera";
                    ((TextBlock)shell.FindName("DetailSubtitle")).Text = "DEV.Camera.Default";
                    ((TextBlock)shell.FindName("ListModeText")).Text = "设备";
                    var tree = (TreeView)shell.FindName("TreeView1");
                    foreach (string name in new[] { "SV6100_Camera", "DEV.Spectrum.Default", "SV6100_Algorithm", "SV6100_Filter", "SV6100_Calibration" })
                        tree.Items.Add(new TreeViewItem { Header = name, IsSelected = name == "SV6100_Camera" });
                    var cameraMarkup = XDocument.Load(Path.Combine(repository.FullName, "Engine/ColorVision.Engine/Services/Devices/Camera/InfoCamera.xaml"));
                    var cameraScroll = (ScrollViewer)XamlReader.Parse(cameraMarkup.Root!.Element(p + "ScrollViewer")!.ToString());
                    var commands = (UniformGrid)cameraScroll.Content;
                    PropertyEditorHelper.GenCommand(new PreviewCommands(), commands);
                    ((Grid)shell.FindName("StackPanelShow")).Children.Add(cameraScroll);
                    foreach (bool terminalPage in new[] { false, true })
                    {
                    if (terminalPage)
                    {
                        var terminal = new ColorVision.Engine.Services.Terminal.TerminalService(new ColorVision.Engine.SysResourceModel { Name = "默认相机服务", Code = "SVR.Camera.Default" });
                        terminal.Config.SendTopic = "RC_local/Camera/SVR.Camera.Default/CMD";
                        terminal.Config.SubscribeTopic = "RC_local/Camera/SVR.Camera.Default/STATUS";
                        terminal.VisualChildren.Add(new PreviewDevice { Name = "SV6100_Camera", Code = "DEV.Camera.Default" });
                        var panel = (Grid)shell.FindName("StackPanelShow");
                        panel.Children.Clear();
                        panel.Children.Add(new ColorVision.Engine.Services.Terminal.TerminalServiceControl(terminal));
                        ((StackPanel)shell.FindName("DetailHeader")).Visibility = Visibility.Collapsed;
                        ((TextBlock)shell.FindName("DetailTitle")).Text = "默认相机服务";
                        ((TextBlock)shell.FindName("DetailSubtitle")).Text = "SVR.Camera.Default";
                    }
                    foreach (int width in new[] { 1040, 820 })
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            shell.Measure(new Size(width, 680));
                            shell.Arrange(new Rect(0, 0, width, 680));
                            shell.UpdateLayout();
                        }
                        var bitmap = new RenderTargetBitmap(width, 680, 96, 96, PixelFormats.Pbgra32);
                        bitmap.Render(shell);
                        Directory.CreateDirectory(output);
                        using var stream = File.Create(Path.Combine(output, $"service-{(terminalPage ? "terminal-" : "")}{(dark ? "dark" : "light")}-{width}.png"));
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        encoder.Save(stream);
                    }
                    }
                }
            }
            finally { Application.Current.Resources = saved; }
        });
    }

    private static void TextElementFont(FrameworkElement shell)
    {
        shell.SetValue(System.Windows.Documents.TextElement.FontFamilyProperty, new FontFamily("Segoe UI, Microsoft YaHei UI"));
        shell.SetValue(System.Windows.Documents.TextElement.FontSizeProperty, 13d);
    }

    private sealed class PreviewDevice : ColorVision.Engine.Services.DeviceService
    {
        public override ColorVision.UI.CopilotBusinessContextBundle CaptureCopilotContext() => throw new NotSupportedException();
    }

    private sealed class PreviewCommands
    {
        [CommandDisplay("修改配置", CategoryOrder = 0), Category("设备与连接"), Description("连接、设备参数与运行配置")]
        public RelayCommand Edit { get; } = new(_ => { });
        [CommandDisplay("管理物理相机", CategoryOrder = 0), Category("设备与连接"), Description("查看与配置物理相机")]
        public RelayCommand Camera { get; } = new(_ => { });
        [CommandDisplay("刷新设备列表", CategoryOrder = 0), Category("设备与连接"), Description("刷新服务端设备列表")]
        public RelayCommand Refresh { get; } = new(_ => { });
        [CommandDisplay("校正文件设置", CategoryOrder = 1), Category("校准与校正"), Description("配置相机关联的校正文件")]
        public RelayCommand Calibration { get; } = new(_ => { });
        [CommandDisplay("本地校正缓存管理", CategoryOrder = 1), Category("校准与校正"), Description("查看和释放本地校正缓存")]
        public RelayCommand Cache { get; } = new(_ => { });
        [CommandDisplay("自动曝光模板", CategoryOrder = 2), Category("采集与显示"), Description("管理自动曝光参数模板")]
        public RelayCommand Exposure { get; } = new(_ => { });
        [CommandDisplay("自动聚焦模板", CategoryOrder = 2), Category("采集与显示"), Description("管理自动聚焦参数模板")]
        public RelayCommand Focus { get; } = new(_ => { });
        [CommandDisplay("相机参数模板", CategoryOrder = 2), Category("采集与显示"), Description("管理相机采集参数模板")]
        public RelayCommand Parameters { get; } = new(_ => { });
        [CommandDisplay("文件保存路径", CategoryOrder = 3), Category("数据与日志"), Description("配置数据与文件的保存位置")]
        public RelayCommand Files { get; } = new(_ => { });
        [CommandDisplay("相机日志", CategoryOrder = 3), Category("数据与日志"), Description("查看相机服务运行日志")]
        public RelayCommand Logs { get; } = new(_ => { });
        [CommandDisplay("重启服务", CategoryOrder = 4), Category("服务与维护"), Description("保存当前配置并重启设备服务")]
        public RelayCommand Restart { get; } = new(_ => { });
        [CommandDisplay("删除", CategoryOrder = 4, CommandType = CommandType.Highlighted), Category("服务与维护"), Description("删除当前设备资源")]
        public RelayCommand Delete { get; } = new(_ => { });
    }
}
