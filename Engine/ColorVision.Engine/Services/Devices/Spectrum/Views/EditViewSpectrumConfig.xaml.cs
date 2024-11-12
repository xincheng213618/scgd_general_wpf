using ColorVision.Themes;
using System;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Spectrum.Views
{


    /// <summary>
    /// EditConfig.xaml 的交互逻辑
    /// </summary>
    public partial class EditViewSpectrumConfig : Window
    {
        public ViewSpectrumConfig Config { get; set; }
        public EditViewSpectrumConfig(ViewSpectrumConfig config)
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
