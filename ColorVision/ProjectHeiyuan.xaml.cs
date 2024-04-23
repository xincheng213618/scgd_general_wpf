using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorVision
{
    /// <summary>
    /// ProjectHeiyuan.xaml 的交互逻辑
    /// </summary>
    public partial class ProjectHeiyuan : Window
    {
        public ProjectHeiyuan()
        {
            InitializeComponent();
        }

        bool result;
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            result = !result;
            ResultText.Text = result ? "OK" : "不合格";
            ResultText.Foreground = result ? Brushes.Green : Brushes.Red;
        }
    }
}
