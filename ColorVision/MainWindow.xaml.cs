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
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            using System.Windows.Forms.OpenFileDialog openFileDialog = new();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png) | *.jpg; *.jpeg; *.png";
            openFileDialog.RestoreDirectory = true;

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;
                ImageShow.Source = new BitmapImage(new Uri(filePath));
                // 在这里处理所选文件的逻辑。
            }
        }

        private void DrawCircle(DrawingContext dc, Brush brush, Pen pen, Point center, double radius)
        {
            dc.DrawEllipse(brush, pen, center, radius, radius);
        }

        private void Render()
        {
            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                Brush brush = Brushes.Blue;
                Pen pen = new Pen(Brushes.Black, 1);
                Point center = new Point(50, 50);
                double radius = 30;

                DrawCircle(dc, brush, pen, center, radius);
            }
            ImageShow.AddVisual(dv);
        }

        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < 100; i++)
            {
                for (int j = 0; j < 100; j++)
                {
                    DrawingVisual dv = new DrawingVisual();
                    using (DrawingContext dc = dv.RenderOpen())
                    {
                        Brush brush = Brushes.Blue;
                        Pen pen = new Pen(Brushes.Black, 1);
                        Point center = new Point(i*50, j*50);
                        double radius = 10;

                        DrawCircle(dc, brush, pen, center, radius);
                    }
                    ImageShow.AddVisual(dv);
                }
            }
            }

    }
}
