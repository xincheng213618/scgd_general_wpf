using ColorVision.Common.MVVM;
using ColorVision.UI;
using cvColorVision;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Services.Devices.Spectrum
{
    /// <summary>
    /// InfoSpectrum.xaml 的交互逻辑
    /// </summary>
    public partial class InfoSpectrum : UserControl, IDisposable
    {
        public DeviceSpectrum Device { get; set; }

        public InfoSpectrum(DeviceSpectrum mqttDeviceSp)
        {
            Device = mqttDeviceSp;
            InitializeComponent();
        }
        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Device;
            PropertyEditorHelper.GenCommand(Device, CommandGrid);
            AppendCorrectionFeatureButtons();
            Device.RefreshEmptySpectrum();
        }

        private void AppendCorrectionFeatureButtons()
        {
            foreach (SpectrumCorrectionFeatureProviderRegistration registration in SpectrumCorrectionFeatureProviderRegistry.Registrations)
            {
                if (CommandGrid.Children.OfType<FrameworkElement>().Any(element => Equals(element.Tag, registration.Metadata.Id)))
                    continue;

                SpectrumCorrectionFeatureMetadata metadata = registration.Metadata;
                var button = new Button
                {
                    Margin = new Thickness(5),
                    Padding = new Thickness(10, 8, 10, 8),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Height = 70,
                    Background = (Brush)Application.Current.FindResource("GlobalBackground"),
                    BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
                    BorderThickness = new Thickness(1),
                    ToolTip = string.IsNullOrWhiteSpace(metadata.Description)
                        ? metadata.DisplayName
                        : $"{metadata.DisplayName}\n{metadata.Description}",
                    Command = new RelayCommand(async _ => await Device.ExecuteCorrectionFeatureAsync(registration.Provider)),
                    Tag = metadata.Id,
                };

                var content = new StackPanel();
                content.Children.Add(new TextBlock
                {
                    Text = metadata.DisplayName,
                    FontSize = 12,
                    FontWeight = FontWeights.Medium,
                    Foreground = (Brush)Application.Current.FindResource("PrimaryTextBrush"),
                    TextWrapping = TextWrapping.Wrap,
                });

                if (!string.IsNullOrWhiteSpace(metadata.Description))
                {
                    content.Children.Add(new TextBlock
                    {
                        Text = metadata.Description,
                        FontSize = 10,
                        Foreground = (Brush)Application.Current.FindResource("SecondaryTextBrush"),
                        Margin = new Thickness(0, 3, 0, 0),
                        Opacity = 0.7,
                        TextWrapping = TextWrapping.Wrap,
                    });
                }

                button.Content = content;
                CommandGrid.Children.Add(button);
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
