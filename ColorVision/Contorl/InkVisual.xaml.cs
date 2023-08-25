using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ColorVision
{

    ///这里是未带开发的部分
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
