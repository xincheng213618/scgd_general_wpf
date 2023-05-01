using ColorVision.Config;
using ColorVision.Extension;
using ColorVision.Info;
using ColorVision.MVVM;
using ColorVision.Util;
using Gu.Wpf.Geometry;
using log4net;
using ScottPlot.Drawing.Colormaps;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
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
        private static readonly ILog log = LogManager.GetLogger(typeof(MainWindow));
        public ImageInfo ImageInfo { get; set; } = new ImageInfo();
        public PerformanceSetting performanceSetting { get; set; } = new PerformanceSetting();

        private ToolBarTop ToolBarTop { get; set; } 
        public MainWindow()
        {
            InitializeComponent();
            ToolBarTop = new ToolBarTop(Zoombox1);
            ToolBar1.DataContext = ToolBarTop;
            ListView1.ItemsSource = DrawingVisualCircleLists;
            StatusBar1.DataContext = performanceSetting;
        }

        private DrawingVisual ImageRuler = new DrawingVisual();

        private  void Button_Click(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png,*.tif) | *.jpg; *.jpeg; *.png;*.tif";
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                log.Info(openFileDialog.FileName);
                string filePath = openFileDialog.FileName;
                BitmapImage bitmapImage = new BitmapImage(new Uri(filePath));

                ImageShow.Source = new BitmapImage(new Uri(filePath));
                DrawGridImage(DrawingVisualGrid, bitmapImage);
                Zoombox1.ZoomUniform();
            }
        }

        public List<DrawingVisualCircle> dctemp { get; set; } = new List<DrawingVisualCircle>();
        public ObservableCollection<DrawingVisualCircle> DrawingVisualCircleLists { get; set; } = new ObservableCollection<DrawingVisualCircle>();


        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < 200; i++)
            {
                for (int j = 0; j < 200; j++)
                {
                    DrawingVisualCircle drawingVisualCircle = new DrawingVisualCircle();
                    drawingVisualCircle.Attribute.Center = new Point(i * 100, j * 100);
                    drawingVisualCircle.Attribute.Radius = 50;

                    drawingVisualCircle.Render();
                    ImageShow.AddVisual(drawingVisualCircle);
                    dctemp.Add(drawingVisualCircle);
                    //DrawingVisualCircleLists.Add(drawingVisualCircle);

                }
            }
            for (int i = 10; i < 20; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    DrawingVisualRectangle drawingVisualCircle = new DrawingVisualRectangle();
                    drawingVisualCircle.Attribute.Rect = new Rect(i * 50, j * 50, 10, 10);
                    drawingVisualCircle.Render();
                    ImageShow.AddVisual(drawingVisualCircle);
                }
            }

            for (int i = 0; i < (dctemp.Count<100? dctemp.Count: 100); i++)
            {
                DrawingVisualCircleLists.Add(dctemp[i]);
            }
            PropertyGrid2.SelectedObject = DrawingVisualCircleLists[0].Attribute;
            DrawingVisualCircleLists[0].Attribute.PropertyChanged += (s, e) =>
            {
                PropertyGrid2.Refresh();
            };
        }

        private DrawingVisual DrawingVisualGrid = new DrawingVisual();

        private void Button5_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggleButton)
            {
                if (!ImageShow.ContainsVisual(DrawingVisualGrid) && toggleButton.IsChecked==true)
                    ImageShow.AddVisual(DrawingVisualGrid);
                if (ImageShow.ContainsVisual(DrawingVisualGrid) && toggleButton.IsChecked == false)
                    ImageShow.RemoveVisual(DrawingVisualGrid);
            }

        }
        private void Button6_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggleButton)
            {
                if (!ImageShow.ContainsVisual(ImageRuler) && toggleButton.IsChecked == true)
                    ImageShow.AddVisual(ImageRuler);
                if (ImageShow.ContainsVisual(ImageRuler) && toggleButton.IsChecked == false)
                    ImageShow.RemoveVisual(ImageRuler);
            }
        }

        private void DrawGridImage(DrawingVisual drawingVisual, BitmapImage bitmapImage)
        {
            Brush brush = Brushes.Black;
            FontFamily fontFamily = new FontFamily("Arial");

            double fontSize = 10;
            using DrawingContext dc = drawingVisual.RenderOpen();
            for (int i = 0; i < bitmapImage.Width; i += 40)
            {
                string text = i.ToString();
                FormattedText formattedText = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(formattedText, new Point(i, -10));
                dc.DrawLine(new Pen(Brushes.Blue, 1), new Point(i, 0), new Point(i, bitmapImage.Height));
            }

            for (int j = 0; j < bitmapImage.Height; j += 40)
            {
                string text = j.ToString();
                FormattedText formattedText = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(formattedText, new Point(-10, j));
                dc.DrawLine(new Pen(Brushes.Blue, 1), new Point(0, j), new Point(bitmapImage.Width, j));
            }
        }





        private void DrawImageRuler()
        {
            if (ImageShow.Source is BitmapImage bitmapImage)
            {
                var actPoint = new Point();
                using DrawingContext dc = ImageRuler.RenderOpen();
                var transform = new MatrixTransform(1 / Zoombox1.ContentMatrix.M11, Zoombox1.ContentMatrix.M12, Zoombox1.ContentMatrix.M21, 1 / Zoombox1.ContentMatrix.M22, (1 - 1 / Zoombox1.ContentMatrix.M11) * actPoint.X, (1 - 1 / Zoombox1.ContentMatrix.M22) * actPoint.Y);
                dc.PushTransform(transform);

                dc.DrawLine(new Pen(Brushes.Red, 10), new Point(100, 50), new Point(200, 50));


                Brush brush = Brushes.Red;
                FontFamily fontFamily = new FontFamily("Arial");
                double fontSize = 10;
                FormattedText formattedText = new FormattedText((1 / Zoombox1.ContentMatrix.M11* bitmapImage.PixelWidth/100).ToString("F2") +"px", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(formattedText, new Point(100, 30));


                double X = 1 / Zoombox1.ContentMatrix.M11 * bitmapImage.PixelWidth / 100;
                double result = X < 10 ? 5 : X < 20 ? 10 : X < 50 ? 20 : X < 100 ? 50 : (X < 200 ? 100 : (X < 500 ? 200 : (X < 1000 ? 500 : (X < 2000 ? 1000 : 2000))));

                dc.DrawLine(new Pen(Brushes.Red, 10), new Point(100, 100), new Point(100+ 100 * result/X, 100));
                FormattedText formattedText1 = new FormattedText((result).ToString("F2") + "px", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(formattedText1, new Point(100, 80));

            }
        }

        private DrawingVisual drawingVisual2 = new DrawingVisual();

        private void DrawImage(Point actPoint, Point disPoint,ImageInfo imageInfo)
        {
            if (ImageShow.Source is BitmapImage bitmapImage)
            {
                if (disPoint.X > 60 && disPoint.X < bitmapImage.PixelWidth - 60 && disPoint.Y > 45 && disPoint.Y < bitmapImage.PixelHeight - 45)
                {
                    CroppedBitmap croppedBitmap = new CroppedBitmap(bitmapImage, new Int32Rect(disPoint.X.ToInt32() - 60, disPoint.Y.ToInt32() - 45, 120, 90));

                    using DrawingContext dc = drawingVisual2.RenderOpen();

                    var transform = new MatrixTransform(1/Zoombox1.ContentMatrix.M11, Zoombox1.ContentMatrix.M12, Zoombox1.ContentMatrix.M21, 1 / Zoombox1.ContentMatrix.M22, (1- 1/Zoombox1.ContentMatrix.M11)*actPoint.X, (1 - 1/Zoombox1.ContentMatrix.M22) * actPoint.Y);
                    dc.PushTransform(transform);

                    dc.DrawImage(croppedBitmap, new Rect(new Point(actPoint.X, actPoint.Y+ 25), new Size(120, 90)));

                    dc.DrawLine(new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00B1FF")), 3), new Point(actPoint.X + 59 , actPoint.Y + 25), new Point(actPoint.X + 59, actPoint.Y + 25 + 90));
                    dc.DrawLine(new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00B1FF")), 3), new Point(actPoint.X, actPoint.Y + 25 +44), new Point(actPoint.X +120, actPoint.Y + 25 + 44));


                    double x1 = actPoint.X;
                    double y1 = actPoint.Y + 25;

                    double width = 120;
                    double height = 90;


                    dc.DrawLine(new Pen(Brushes.Black, 0.5), new Point(x1,y1-0.25), new Point(x1,y1+height+0.25));
                    dc.DrawLine(new Pen(Brushes.Black, 0.5), new Point(x1, y1), new Point(x1 + width, y1));
                    dc.DrawLine(new Pen(Brushes.Black, 0.5), new Point(x1 + width, y1-0.25), new Point(x1 + width, y1 + height+0.25));
                    dc.DrawLine(new Pen(Brushes.Black, 0.5), new Point(x1, y1 + height),new Point(x1 + width, y1 + height));

                    x1 = x1+1;
                    y1 = y1 + 1;
                    width -= 2;
                    height -= 2;
                    dc.DrawLine(new Pen(Brushes.White, 1.5), new Point(x1, y1-0.75), new Point(x1, y1 + height + 0.75));
                    dc.DrawLine(new Pen(Brushes.White, 1.5), new Point(x1, y1), new Point(x1 + width, y1));
                    dc.DrawLine(new Pen(Brushes.White, 1.5), new Point(x1 + width, y1- 0.75), new Point(x1 + width, y1 + height+ 0.75));
                    dc.DrawLine(new Pen(Brushes.White, 1.5), new Point(x1, y1 + height), new Point(x1 + width, y1 + height));

                    dc.DrawRectangle(Brushes.Black,new Pen(Brushes.White, 0),new Rect(x1-1, y1 + height + 1, width+2, 45));

                    Brush brush = Brushes.White;
                    FontFamily fontFamily = new FontFamily("Arial");
                    double fontSize = 10;
                    FormattedText formattedText = new FormattedText($"R:{imageInfo.R}  G:{imageInfo.G}  B:{imageInfo.B}", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                    dc.DrawText(formattedText, new Point(x1+5, y1+ height+5));
                    FormattedText formattedTex1 = new FormattedText($"({imageInfo.X},{imageInfo.Y})", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                    dc.DrawText(formattedTex1, new Point(x1 + 5, y1 + height + 31));

                    FormattedText formattedTex3 = new FormattedText($"{imageInfo.Hex}", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                    dc.DrawText(formattedTex3, new Point(x1 + 5, y1 + height + 18));
                    dc.Pop();
                    if (drawingVisual2.Effect is not DropShadowEffect)
                        drawingVisual2.Effect = new DropShadowEffect() { Opacity = 0.5 };

                }

            }
        }

        private DrawingVisual SelectRect = new DrawingVisual();

        private static void DrawSelectRect(DrawingVisual drawingVisual, Rect rect)
        {
            using DrawingContext dc = drawingVisual.RenderOpen();
            dc.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#77F3F3F3")), new Pen(Brushes.Blue, 1), rect);
        }

        private Point MouseDownP;
        private DrawingVisualCircle? SelectDCircle;
        private void ImageShow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas && !Keyboard.Modifiers.HasFlag(Zoombox1.ActivateOn))
            {
                MouseDownP = e.GetPosition(drawCanvas);
                drawCanvas.CaptureMouse();

                if (EraseVisual)
                {
                    DrawSelectRect(SelectRect, new Rect(MouseDownP, MouseDownP)); ;
                    drawCanvas.AddVisual(SelectRect);
                }
                else
                {
                    if (drawCanvas.GetVisual(MouseDownP) is DrawingVisualCircle drawingVisual)
                    {
                        if (PropertyGrid2.SelectedObject is CircleAttribute viewModelBase)
                        {
                            viewModelBase.PropertyChanged -= (s, e) =>
                            {
                                PropertyGrid2.Refresh();
                            };
                            viewModelBase.Pen = new Pen(Brushes.Black, 1);
                        }

                        PropertyGrid2.SelectedObject = drawingVisual.Attribute;
                        drawingVisual.Attribute.Pen = new Pen(Brushes.Red, 2);
                        drawingVisual.Attribute.PropertyChanged += (s, e) =>
                        {
                            PropertyGrid2.Refresh();
                        };

                        if (ToggleButtonDrag.IsChecked == true)
                            SelectDCircle = drawingVisual;
                    }

                    if (drawCanvas.GetVisual(MouseDownP) is DrawingVisualRectangle drawingVisual1)
                    {
                        if (PropertyGrid2.SelectedObject is ViewModelBase viewModelBase)
                        {
                            viewModelBase.PropertyChanged -= (s, e) =>
                            {
                                PropertyGrid2.Refresh();
                            };
                        }

                        PropertyGrid2.SelectedObject = drawingVisual1.Attribute;
                        drawingVisual1.Attribute.PropertyChanged += (s, e) =>
                        {
                            PropertyGrid2.Refresh();
                        };
                    }
                }

            }
        }

        private void ImageShow_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas&&!Keyboard.Modifiers.HasFlag(Zoombox1.ActivateOn))
            {
                var point = e.GetPosition(drawCanvas);

                var controlWidth = drawCanvas.ActualWidth;
                var controlHeight = drawCanvas.ActualHeight;

                if (EraseVisual)
                {
                    DrawSelectRect(SelectRect, new Rect(MouseDownP,point)); ;
                }
                else if (SelectDCircle is not null)
                {
                    SelectDCircle.Attribute.Center = point;
                }
                if (drawCanvas.Source is BitmapImage bitmapImage)
                {
                    int imageWidth = bitmapImage.PixelWidth;
                    int imageHeight = bitmapImage.PixelHeight;

                    var actPoint = new Point(point.X, point.Y);


                    point.X = point.X / controlWidth * imageWidth;
                    point.Y = point.Y / controlHeight * imageHeight;

                    var bitPoint = new Point(point.X.ToInt32(), point.Y.ToInt32());


                    if (point.X.ToInt32() >= 0 && point.X.ToInt32() < bitmapImage.PixelWidth && point.Y.ToInt32() >= 0 && point.Y.ToInt32() < bitmapImage.PixelHeight)
                    {
                        var color = bitmapImage.GetPixelColor(point.X.ToInt32(), point.Y.ToInt32());
                        ImageInfo.X = point.X.ToInt32();
                        ImageInfo.Y = point.Y.ToInt32();
                        ImageInfo.X1 = point.X;
                        ImageInfo.Y1 = point.Y;

                        ImageInfo.R = color.R;
                        ImageInfo.G = color.G;
                        ImageInfo.B = color.B;

                        ImageInfo.Color = new SolidColorBrush(color);
                        ImageInfo.Hex = color.ToHex();
                    }

                    DrawImage(actPoint, bitPoint, ImageInfo);
                }
            }
        }
        private void ImageShow_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas && !Keyboard.Modifiers.HasFlag(Zoombox1.ActivateOn))
            {
                var MouseUpP = e.GetPosition(drawCanvas);
                if (EraseVisual)
                {
                    foreach (var item in drawCanvas.GetVisuals(new RectangleGeometry(new Rect(MouseDownP, MouseUpP))))
                    {
                        drawCanvas.RemoveVisual(item);
                    }
                    ;
                    drawCanvas.RemoveVisual(SelectRect);
                }

                drawCanvas.ReleaseMouseCapture();
                SelectDCircle = null;
            }
        }

        private void ImageShow_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            DrawImageRuler();
        }

        private void ImageShow_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas && !drawCanvas.ContainsVisual(drawingVisual2))
            {
                ImageShow.AddVisual(drawingVisual2);
            }
        }
        private void ImageShow_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas && drawCanvas.ContainsVisual(drawingVisual2))
            {
                ImageShow.RemoveVisual(drawingVisual2);
            }
        }

        private void Button3_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Aoi配置文件测试");
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "配置文件(*.cfg) | *.cfg";
            openFileDialog.RestoreDirectory = true;

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                log.Info("打开AoiCfg:" +openFileDialog.FileName);
                AoiCfg aoiCfg = CfgFile.LoadCfgFile<AoiCfg>(openFileDialog.FileName);
                PropertyGrid2.SelectedObject = aoiCfg;
            }


        }

        private void Button4_Click(object sender, RoutedEventArgs e)
        {
            if (PropertyGrid2.SelectedObject is AoiCfg aoiCfg)
            {
                using var saveFileDialog = new System.Windows.Forms.SaveFileDialog();
                saveFileDialog.Filter = "Configuration file (*.cfg)|*.cfg";
                saveFileDialog.FileName = "aoi.cfg";
                if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    CfgFile.SaveCfgFile(saveFileDialog.FileName, aoiCfg);
            }
        }

        private bool EraseVisual;


        private void Button7_Click(object sender, RoutedEventArgs e)
        {
            if(sender is ToggleButton toggleButton)
            {
                EraseVisual = toggleButton.IsChecked == true;
                if (EraseVisual)
                {
                    ToggleButtonDrag.IsChecked = true;
                    Zoombox1.ActivateOn = toggleButton.IsChecked == true ? ModifierKeys.Control : ModifierKeys.None;
                }
            }
        }





        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox&& checkBox.Tag is DrawingVisualCircle drawingVisualCircle)
            {
                if (checkBox.IsChecked ==true)
                {
                    if (!ImageShow.ContainsVisual(drawingVisualCircle))
                        ImageShow.AddVisual(drawingVisualCircle);
                }
                else
                {
                    if (ImageShow.ContainsVisual(drawingVisualCircle))
                        ImageShow.RemoveVisual(drawingVisualCircle);
                }
            }
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            MQTTDemo mQTTDemo = new MQTTDemo();
            mQTTDemo.Owner = this;
            mQTTDemo.Show();
        }


        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {

            
        }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggleButton)
            {
                Zoombox1.ActivateOn = toggleButton.IsChecked ==true? ModifierKeys.Control: ModifierKeys.None;
            }
        }

        private void Button8_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggleButton)
            {
                var mainWindow = Application.Current.MainWindow;

                if (toggleButton.IsChecked == true)
                {
                    mainWindow.WindowStyle = WindowStyle.None;
                    mainWindow.WindowState = WindowState.Maximized;
                    if (mainWindow.Content is Grid grid)
                    {
                        if (grid.Children[0] is Menu menu)
                        {
                            menu.Visibility = Visibility.Collapsed;
                        }
                    }

                    ContentGrid.Children.Remove(ZoomGrid);
                    mainWindow.Content = ZoomGrid;
                }
                else
                {
                    mainWindow.WindowStyle = WindowStyle.SingleBorderWindow; // 或者其他您需要的风格
                    mainWindow.WindowState = WindowState.Normal;
                    mainWindow.ResizeMode = ResizeMode.CanResize; // 如果需要允许改变大小
                    mainWindow.Topmost = false; // 如果之前设置了 Topmost，现在取消
                    mainWindow.ShowInTaskbar = true; // 如果之前设置了 ShowInTaskbar，现在取消

                    mainWindow.Content = Root;
                    ContentGrid.Children.Add(ZoomGrid);


                }

            }



        }

        private void ToolBar1_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ToolBar toolBar)
            {
                if (toolBar.Template.FindName("OverflowGrid", toolBar) is FrameworkElement overflowGrid )
                    overflowGrid.Visibility = Visibility.Collapsed;
                if (toolBar.Template.FindName("MainPanelBorder", toolBar) is FrameworkElement mainPanelBorder)
                    mainPanelBorder.Margin = new Thickness(0);
            }
        }
    }




    public class ImageInfo : ViewModelBase
    {
        private int _X;
        public int X { get => _X; set { _X = value; NotifyPropertyChanged(); } }
        private int _Y;
        public int Y { get => _Y; set { _Y = value; NotifyPropertyChanged(); } }

        private double _X1;
        public double X1 { get => _X1 ; set { _X1 = value; NotifyPropertyChanged(); } }
        private double _Y1;
        public double Y1 { get => _Y1; set { _Y1 = value; NotifyPropertyChanged(); } }


        private int _R;
        public int R { get => _R; set { _R = value; NotifyPropertyChanged(); } }
        private int _G;

        public int G { get => _G; set { _G = value; NotifyPropertyChanged(); } }

        private int _B;
        public int B { get => _B; set {  _B = value; NotifyPropertyChanged(); } }

        private string _Hex;
        public string Hex { get => _Hex; set { _Hex = value; NotifyPropertyChanged(); } }

        private SolidColorBrush _Color;
        public SolidColorBrush Color { get => _Color; set { _Color = value; NotifyPropertyChanged(); } }
       
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
