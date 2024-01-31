using System.Windows;

namespace ColorVision
{
    /// <summary>
    /// ChangelogWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ChangelogWindow : Window
    {
        public ChangelogWindow()
        {
            InitializeComponent();
        }
        public void SetChangelogText(string text)
        {
            ChangelogTextBlock.Text = text;
        }   
    }
}
