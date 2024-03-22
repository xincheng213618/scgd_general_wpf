using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Solution.View
{
    /// <summary>
    /// SolutionView.xaml 的交互逻辑
    /// </summary>
    public partial class SolutionView : UserControl
    {
        public SolutionView()
        {
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, System.EventArgs e)
        {
            MainFrame.Navigate(new HomePage(MainFrame));
        }
    }
}
