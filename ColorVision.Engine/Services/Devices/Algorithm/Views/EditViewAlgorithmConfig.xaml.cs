using ColorVision.Themes;
using System;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Algorithm.Views
{
    /// <summary>
    /// EditConfig.xaml 的交互逻辑
    /// </summary>
    public partial class EditViewAlgorithmConfig : Window
    {
        public ViewAlgorithmConfig Config { get; set; }
        public EditViewAlgorithmConfig(ViewAlgorithmConfig config)
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
