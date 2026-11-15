using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Properties;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.EditorTools.AppCommand;

internal sealed class ImageSettingsEditorTool(EditorContext context) : IEditorCustomControlTool
{
    public ToolBarLocal ToolBarLocal => ToolBarLocal.Left;
    public string GuidId => "ImageViewSettings";
    public int Order => 4;
    public bool SupportsEmptyView => true;
    public object? Icon => null;
    public ICommand Command { get; } = new RelayCommand(_ => context.OpenSettingsWindow(Resources.Settings_GroupDisplay));

    public FrameworkElement CreateToolControl()
    {
        var button = new Button
        {
            Width = 27,
            Height = 27,
            Padding = new Thickness(3),
            Command = Command,
            ToolTip = Resources.ImageView_ContextSettings,
            Content = new TextBlock { FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 16, Text = "\uE713" }
        };
        AutomationProperties.SetName(button, Resources.ImageView_ContextSettings);
        return button;
    }
}
