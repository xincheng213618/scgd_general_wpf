using ColorVision.Themes;
using System;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Views
{
    /// <summary>
    /// EditConfig.xaml 的交互逻辑
    /// </summary>
    public partial class EditViewThirdPartyAlgorithmsConfig : Window
    {
        public ViewThirdPartyAlgorithmsConfig Config { get; set; }

        public EditViewThirdPartyAlgorithmsConfig(ViewThirdPartyAlgorithmsConfig config)
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
