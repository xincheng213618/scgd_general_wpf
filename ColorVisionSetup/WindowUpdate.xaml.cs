using System;
using System.Windows;

namespace ColorVisionSetup
{
    /// <summary>
    /// WindowUpdate.xaml 的交互逻辑
    /// </summary>
    public partial class WindowUpdate : Window
    {
        public WindowUpdate()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            DataContext = AutoUpdater.GetInstance();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (MessageBox.Show($"是否停止更新", "ColorVision", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
            {

            }
            else
            {
                e.Cancel =true;
            }

        }
    }
}
