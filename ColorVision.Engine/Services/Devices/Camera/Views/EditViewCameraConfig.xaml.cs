using ColorVision.Themes;
using System;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Camera.Views
{
    /// <summary>
    /// EditConfig.xaml 的交互逻辑
    /// </summary>
    public partial class EditViewCameraConfig : Window
    {
        public ViewCameraConfig Config { get; set; }
        public EditViewCameraConfig(ViewCameraConfig config)
        {
            Config = config;
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Config;
        }
    }
}
