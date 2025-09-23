using ColorVision.ImageEditor.Draw.Ruler;
using System.Windows;
using System;
using ColorVision.Themes;
using System.Windows.Media.Imaging;
using ColorVision.ImageEditor.Draw.Special;

namespace ColorVision.ImageEditor
{

    public class CIEColorConverter
    {
        // 线性化RGB值
        private static double Linearize(double value)
        {
            value = value / 255.0;

            if (value <= 0.04045)
            {
                return value / 12.92;
            }
            else
            {
                return Math.Pow((value + 0.055) / 1.055, 2.4);
            }
        }

        // 转换RGB到XYZ
        private static double[] RgbToXyz(int r, int g, int b)
        {
            // 线性化RGB值
            double R = Linearize(r);
            double G = Linearize(g);
            double B = Linearize(b);

            // 使用sRGB的转换矩阵
            double X = R * 0.4124 + G * 0.3576 + B * 0.1805;
            double Y = R * 0.2126 + G * 0.7152 + B * 0.0722;
            double Z = R * 0.0193 + G * 0.1192 + B * 0.9505;

            return new double[] { X, Y, Z };
        }

        // 从XYZ到CIE 1931 xy色度坐标
        private static double[] XyzToCie1931xy(double[] xyz)
        {
            double X = xyz[0];
            double Y = xyz[1];
            double Z = xyz[2];

            double x = X / (X + Y + Z);
            double y = Y / (X + Y + Z);

            return new double[] { x, y };
        }

        public static double[] RgbToCie1931xy(int r, int g, int b)
        {
            double[] xyz = RgbToXyz(r, g, b);
            return XyzToCie1931xy(xyz);
        }
    }

    /// <summary>
    /// WindowCIE.xaml 的交互逻辑
    /// </summary>
    public partial class WindowCIE : Window
    {
        public WindowCIE()
        {
            InitializeComponent();
            this.ApplyCaption();
        }
        public ImageViewModel ImageViewModel => ImageView.ImageViewModel;
        private void Window_Initialized(object sender, System.EventArgs e)
        {
            ImageView.ToolBar1.Visibility = Visibility.Collapsed;
            ImageView.ToolBarRight.Visibility = Visibility.Collapsed;
            ImageView.ToolBarLeft.Visibility = Visibility.Collapsed;
            ImageView.ToolBarAl.Visibility = Visibility.Collapsed;

            ImageViewModel.Crosshair.IsShow = true;
            ImageView.SetImageSource(new BitmapImage(new Uri("/ColorVision.ImageEditor;component/Assets/Image/CIE1931xy.png", UriKind.Relative)));
            ImageViewModel.ToolBarScaleRuler.IsShow = false;

            ImageView.ComboBoxLayers.Visibility = Visibility.Collapsed;
            ImageView.Zoombox1.ZoomUniform();
        }

        public void ChangeSelect(double x,double y)
        {
            if (!ImageViewModel.Crosshair.IsShow)
                ImageViewModel.Crosshair.IsShow = true;

            x = 60 + 755 * x;
            y = 689 - 755 * y;
            x = x / ImageViewModel.Crosshair.Ratio;
            y = y / ImageViewModel.Crosshair.Ratio;
            ImageViewModel.Crosshair.DrawImage(new Point(x, y));
        }


        public void ChangeSelect(ImageInfo imageInfo)
        {
            if (!ImageViewModel.Crosshair.IsShow)
                ImageViewModel.Crosshair.IsShow = true;

            double[] doubles = CIEColorConverter.RgbToCie1931xy(imageInfo.R,imageInfo.G,imageInfo.B);
            double x =60 + 755 * doubles[0];
            double Y = 689 - 755 * doubles[1];
            x = x / ImageViewModel.Crosshair.Ratio;
            Y = Y / ImageViewModel.Crosshair.Ratio;

            ImageViewModel.Crosshair.DrawImage(new Point(x, Y));
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ImageView.ImageShow.Source = new BitmapImage(new Uri("/ColorVision.ImageEditor;component/Assets/Image/cie_1976_ucs.png", UriKind.Relative));
            ImageViewModel.ZoomUniformToFill.Execute(sender);
        }
    }
}
