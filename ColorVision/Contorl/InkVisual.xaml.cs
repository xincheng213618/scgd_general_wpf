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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ColorVision
{
    /// <summary>
    /// InkCanvas.xaml 的交互逻辑
    /// </summary>
    public partial class InkVisual : UserControl
    {
        public InkVisual()
        {
            InitializeComponent();
        }

        private void inkCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Ellipse ellipse = new Ellipse()
            {
                Width = 50,
                Height = 50,
                Fill = Brushes.Black
            };

            InkCanvas.SetLeft(ellipse, e.GetPosition(inkCanvas).X - ellipse.Width / 2);
            InkCanvas.SetTop(ellipse, e.GetPosition(inkCanvas).Y - ellipse.Height / 2);

            inkCanvas.Children.Add(ellipse);
        }
    }
}
