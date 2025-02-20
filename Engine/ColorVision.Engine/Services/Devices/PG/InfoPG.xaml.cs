using ColorVision.UI.Extension;
using System;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ColorVision.Engine.Services.Devices.PG
{
    /// <summary>
    /// InfoPG.xaml 的交互逻辑
    /// </summary>
    public partial class InfoPG : UserControl
    {
        public DevicePG DevicePG { get; set; }
        public InfoPG(DevicePG devicePG)
        {
            DevicePG = devicePG;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = DevicePG;

            var properties = DevicePG.GetType().GetProperties();

            foreach (var property in properties)
            {
                var attribute = property.GetCustomAttribute<CommandDisplayAttribute>();
                if (attribute != null)
                {
                    var command = property.GetValue(DevicePG) as ICommand;
                    if (command != null)
                    {
                        var button = new Button
                        {
                            Style = (Style)FindResource("ButtonCommand"),
                            Content = attribute.DisplayName,
                            Command = command
                        };
                        CommandGrid.Children.Add(button);
                    }
                }
            }
        }

        private void UniformGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is UniformGrid uniformGrid)
            {
                uniformGrid.AutoUpdateLayout();
            }
        }
    }
}
