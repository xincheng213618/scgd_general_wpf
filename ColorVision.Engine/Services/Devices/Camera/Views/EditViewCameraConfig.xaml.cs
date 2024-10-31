using ColorVision.Themes;
using ColorVision.UI;
using System;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Camera.Views
{
    /// <summary>
    /// EditConfig.xaml 的交互逻辑
    /// </summary>
    public partial class EditViewCameraConfig : Window
    {
        public IConfig Config { get; set; }
        public EditViewCameraConfig(IConfig config)
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
