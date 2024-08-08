using ColorVision.Themes;
using System.Windows;

namespace ColorVisonChat
{
    /// <summary>
    /// ChatGPTConfigSetting.xaml 的交互逻辑
    /// </summary>
    public partial class ChatGPTConfigSetting : Window
    {
        public ChatGPTConfigSetting()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = ChatGPTConfig.Instance;
        }
    }
}
