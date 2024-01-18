using System;
using System.Windows;

namespace ColorVision.Update
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
            this.DataContext = AutoUpdater.GetInstance();
        }
    }
}
