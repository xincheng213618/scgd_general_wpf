using ColorVision.Themes;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services
{
    public partial class DevicePropertyWindow : Window
    {
        internal DevicePropertyWindow(string? name, string? code, UserControl content)
        {
            InitializeComponent();
            DeviceTitle.Text = string.IsNullOrWhiteSpace(name) ? Properties.Resources.Property : name;
            DeviceCode.Text = code ?? string.Empty;
            DeviceCode.Visibility = string.IsNullOrWhiteSpace(code) ? Visibility.Collapsed : Visibility.Visible;
            DeviceContent.Content = content;
            this.ApplyCaption();
        }
    }
}
