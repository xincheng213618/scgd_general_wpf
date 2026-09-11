using ColorVision.Themes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Services
{
    public partial class DevicePropertyWindow : Window
    {
        internal DevicePropertyWindow(ImageSource? icon, string? name, string? code, UserControl content)
        {
            InitializeComponent();
            Icon = icon;
            DeviceIcon.Source = icon;
            DeviceIcon.Visibility = icon == null ? Visibility.Collapsed : Visibility.Visible;
            DeviceTitle.Text = string.IsNullOrWhiteSpace(name) ? Properties.Resources.Property : name;
            DeviceCode.Text = code ?? string.Empty;
            DeviceCode.Visibility = string.IsNullOrWhiteSpace(code) ? Visibility.Collapsed : Visibility.Visible;
            DeviceContent.Content = content;
            this.ApplyCaption();
        }
    }
}
