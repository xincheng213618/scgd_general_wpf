using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Ink;
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
    /// 
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

        List<DrawingVisualCircle> dvList = new List<DrawingVisualCircle>();

        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    DrawingVisualCircle drawingVisualCircle = new DrawingVisualCircle();
                    drawingVisualCircle.Attribute.Center = new Point(i * 50, j * 50);
                    drawingVisualCircle.Render();
                    dvList.Add(drawingVisualCircle);
                    ImageShow.AddVisual(drawingVisualCircle);
                }
            }
            PropertyGrid2.SelectedObject = dvList[0].Attribute;
            dvList[0].Attribute.PropertyChanged += (s, e) =>
            {
                PropertyGrid2.Refresh();
            };

        }

        private void Button2_Click(object sender, RoutedEventArgs e)
        {
            foreach (var dv in dvList)
            {
                if (dv is DrawingVisualCircle visualCircle)
                {
                    visualCircle.Attribute.Brush = Brushes.Red;
                    visualCircle.Attribute.Center = new Point() { X= visualCircle.Attribute.Center.X+10, Y= visualCircle.Attribute.Center.Y+10 };
                    visualCircle.Render();
                }
            }
        }

        private void ImageShow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas)
            {
                Point point = e.GetPosition(drawCanvas);
                if (drawCanvas.GetVisual(point) is DrawingVisualCircle drawingVisual)
                {
                    if (PropertyGrid2.SelectedObject is CircleAttribute circle)
                    {
                        circle.PropertyChanged -= (s, e) =>
                        {
                            PropertyGrid2.Refresh();
                        };  
                    }

                    PropertyGrid2.SelectedObject = drawingVisual.Attribute;
                    drawingVisual.Attribute.PropertyChanged += (s, e) =>
                    {
                        PropertyGrid2.Refresh();
                    };
                }
            }

        }
    }

    public class CustomStroke: Stroke
    {
        public CustomStroke(StylusPointCollection stylusPoints) : base(stylusPoints) 
        {
        }
        public CustomStroke(StylusPointCollection stylusPoints, DrawingAttributes drawingAttributes) : base(stylusPoints, drawingAttributes)
        { 
        }
        protected override void DrawCore(DrawingContext drawingContext, DrawingAttributes drawingAttributes)
        {
            base.DrawCore(drawingContext, drawingAttributes);
        }
    }
}
