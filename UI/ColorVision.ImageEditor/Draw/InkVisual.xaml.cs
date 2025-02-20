using System;
using System.IO;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorVision.ImageEditor.Draw
{

    ///可以在编辑的图像控件
    public partial class InkVisual : UserControl
    {

        public ImageViewModel ToolBarTop { get; set; }

        public InkVisual()
        {
            InitializeComponent();
            ToolBarTop = new ImageViewModel(this,Zoombox1,drawCanvas);
            TooBar1.DataContext = ToolBarTop;
        }

        private void inkCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Ellipse ellipse = new()
            {
                Width = 50,
                Height = 50,
                Fill = Brushes.Black
            };

            InkCanvas.SetLeft(ellipse, e.GetPosition(inkCanvas).X - ellipse.Width / 2);
            InkCanvas.SetTop(ellipse, e.GetPosition(inkCanvas).Y - ellipse.Height / 2);
            inkCanvas.Children.Add(ellipse);

        }

        private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                OpenImage(openFileDialog.FileName);
            }
        }





        public void OpenImage(string? filePath)
        {
            if (filePath != null && File.Exists(filePath))
            {
                BitmapImage bitmapImage = new(new Uri(filePath));
                drawCanvas.Source = bitmapImage;
                Zoombox1.ZoomUniform();
            }
        }
    }
}
