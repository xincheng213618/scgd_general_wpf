using ColorVision.Themes;
using System.Windows;

namespace ProjectLUX.Process
{

    /// <summary>
    /// BatchProcessManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ProcessManagerWindow : Window
    {
        public ProcessManagerWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {

        }

        private void ComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }
    }
}
