using ColorVision.Themes;
using System;
using System.Windows;

namespace ColorVision.Engine.Templates
{
    /// <summary>
    /// EditConfig.xaml 的交互逻辑
    /// </summary>
    public partial class TemplateEditWindowSetting : Window
    {
        public TemplateWindowSetting Config { get; set; }
        public TemplateEditWindowSetting(TemplateWindowSetting config)
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
