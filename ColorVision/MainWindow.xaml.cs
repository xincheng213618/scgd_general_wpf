using ColorVision.Config;
using ColorVision.Extension;
using ColorVision.Info;
using ColorVision.MQTT;
using ColorVision.MVVM;
using ColorVision.Template;
using ColorVision.Util;
using log4net;
using OpenCvSharp.Internal;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
        public PerformanceSetting PerformanceSetting { get; set; } = new PerformanceSetting();

        public ToolBarTop ToolBarTop { get; set; } 

        public MQTTControl MQTTControl { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            ToolBarTop = new ToolBarTop(Zoombox1,ImageShow);
            ToolBar1.DataContext = ToolBarTop;
            ListView1.ItemsSource = DrawingVisualCircleLists;
            StatusBarItem1.DataContext = PerformanceSetting;
            StatusBarItem2.DataContext = PerformanceSetting;
            MQTTControl = MQTTControl.GetInstance(); ;
            Application.Current.MainWindow = this;
            this.Closed += (s, e) =>
            {
                Environment.Exit(0);
            };

        }
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr handle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(handle).AddHook(new HwndSourceHook(WndProc));
        }

        private async void Window_Initialized(object sender, EventArgs e)
        {
            if (MainWindowConfig.IsExist)
            {
                this.Icon = MainWindowConfig.Icon ?? this.Icon;
                this.Title = MainWindowConfig.Title ?? this.Title;
            }
            await Task.Delay(100);
            ToolBar1.Visibility = Visibility.Collapsed;
        }

        const uint WM_USER = 0x0400; // 用户自定义消息起始值

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int GlobalGetAtomName(ushort nAtom, char[] retVal, int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern short GlobalDeleteAtom(short nAtom);

        IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_USER+1)
            {
                try
                {
                    char[] chars = new char[1024];
                    int size = GlobalGetAtomName((ushort)wParam, chars, chars.Length);
                    if (size > 0)
                    {

                        string result = new string(chars, 0, size);
                        MessageBox.Show(result);
                        GlobalDeleteAtom((short)wParam);
                    }
                }
                catch (Exception ex){ MessageBox.Show(ex.Message);}
            }
            return IntPtr.Zero;
        }


        private DrawingVisual ImageRuler = new DrawingVisual();

        private  void Button_Click(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png,*.tif) | *.jpg; *.jpeg; *.png;*.tif";
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;
                OpenImage(filePath);
            }
        }

        public void OpenImage(string? filePath)
        {
            if (filePath!=null&&File.Exists(filePath))
            {
                log.Info(filePath);
                BitmapImage bitmapImage = new BitmapImage(new Uri(filePath));

                ImageShow.Source = new BitmapImage(new Uri(filePath));
                DrawGridImage(DrawingVisualGrid, bitmapImage);
                Zoombox1.ZoomUniform();
                ToolBar1.Visibility = Visibility.Visible;
            }
        }



        public ObservableCollection<DrawingVisualCircle> DrawingVisualCircleLists { get; set; } = new ObservableCollection<DrawingVisualCircle>();


        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < 50; i++)
            {
                for (int j = 0; j < 50; j++)
                {
                    DrawingVisualCircle drawingVisualCircle = new DrawingVisualCircle();
                    drawingVisualCircle.Attribute.Center = new Point(i * 50, j * 50);
                    drawingVisualCircle.Attribute.Radius = 20;
                    drawingVisualCircle.Attribute.ID = i*50+j;
                    drawingVisualCircle.Render();
                    ImageShow.AddVisual(drawingVisualCircle);
                    DrawingVisualCircleLists.Add(drawingVisualCircle);

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
            PropertyGrid2.SelectedObject = DrawingVisualCircleLists[0].Attribute;
            DrawingVisualCircleLists[0].Attribute.PropertyChanged += (s, e) =>
            {
                PropertyGrid2.Refresh();
            };

            ImageGroupGrid.Visibility = Visibility.Visible;
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
                        drawingVisual.Attribute.Pen = new Pen(Brushes.Red, 3);
                        drawingVisual.Attribute.PropertyChanged += (s, e) =>
                        {
                            PropertyGrid2.Refresh();
                        };
                        drawCanvas.TopVisual(drawingVisual);

                        ListView1.ScrollIntoView(drawingVisual);
                        ListView1.SelectedIndex = DrawingVisualCircleLists.IndexOf(drawingVisual);

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
            if (sender is DrawCanvas drawCanvas&&( Zoombox1.ActivateOn ==ModifierKeys.None|| !Keyboard.Modifiers.HasFlag(Zoombox1.ActivateOn)))
            {
                var point = e.GetPosition(drawCanvas);

                var controlWidth = drawCanvas.ActualWidth;
                var controlHeight = drawCanvas.ActualHeight;

                if (EraseVisual)
                {
                    DrawSelectRect(SelectRect, new Rect(MouseDownP,point)); ;
                }
                else if (SelectDCircle != null)
                {
                    SelectDCircle.Attribute.Center = point;
                }

                if (ToolBarTop.Move&&drawCanvas.Source is BitmapImage bitmapImage)
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

                    ToolBarTop.DrawImage(actPoint, bitPoint, ImageInfo);
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
            ToolBarTop.DrawVisualImageControl(true);
        }
        private void ImageShow_MouseLeave(object sender, MouseEventArgs e)
        {
            ToolBarTop.DrawVisualImageControl(false);
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
                    {
                        ImageShow.AddVisual(drawingVisualCircle);
                    }

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
            MQTTLog MQTTLog = new MQTTLog();
            MQTTLog.Owner = this;
            MQTTLog.Show();
        }


        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {

            
        }


        private void Button8_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggleButton)
            {
                var mainWindow = this;

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

                    ImageContentGrid.Children.Remove(ZoomGrid);
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
                    ImageContentGrid.Children.Add(ZoomGrid);
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


        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1 && DrawingVisualCircleLists[listView.SelectedIndex] is DrawingVisualCircle drawingVisual)
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
                drawingVisual.Attribute.Pen = new Pen(Brushes.Red, 3);
                drawingVisual.Attribute.PropertyChanged += (s, e) =>
                {
                    PropertyGrid2.Refresh();
                };
                ImageShow.TopVisual(drawingVisual);
            }
        }



        private MQTTCamera MQTTCamera { get; set; }
        private void StackPanelCamera_Initialized(object sender, EventArgs e)
        {

            MQTTCamera = new MQTTCamera();
            MQTTCamera.FileHandler += OpenImage;

            ComboxCameraType.ItemsSource = from e1 in Enum.GetValues(typeof(CameraType)).Cast<CameraType>()
                                           select new KeyValuePair<CameraType, string>(e1, e1.ToDescription());
            ComboxCameraType.SelectedIndex = 2;

            ComboxCameraType.SelectionChanged += (s, e) =>
            {
                if (ComboxCameraType.SelectedItem is KeyValuePair<CameraType, string> KeyValue)
                {
                    if (KeyValue.Key == CameraType.BVQ)
                    {
                        StackPanelFilterWheel.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        if (StackPanelFilterWheel.Visibility == Visibility.Visible)
                        {
                            StackPanelFilterWheel.Visibility = Visibility.Collapsed;
                        }
                    }
                }
            };


            ComboxCameraTakeImageMode.ItemsSource = from e1 in Enum.GetValues(typeof(TakeImageMode)).Cast<TakeImageMode>()
                                                    select new KeyValuePair<TakeImageMode, string>(e1, e1.ToDescription());
            ComboxCameraTakeImageMode.SelectedIndex = 0;



            StackPanelOpen.Visibility = Visibility.Collapsed;


            MQTTCamera.InitCameraSuccess += (s, e) =>
            {
                ComboxCameraID.ItemsSource = MQTTCamera.CameraID?.IDs;
                ComboxCameraID.SelectedIndex = 0;
                StackPanelOpen.Visibility = Visibility.Visible;
            };
        }

        private void StackPanelCalibration_Initialized(object sender, EventArgs e)
        {
            ComboxCalibrationTemplate.ItemsSource = TemplateControl.GetInstance().CalibrationParams;
            ComboxCalibrationTemplate.SelectionChanged += (s, e) =>
            {
                if (ComboxCalibrationTemplate.SelectedItem is KeyValuePair<string, CalibrationParam> KeyValue && KeyValue.Value is CalibrationParam calibrationParam)
                {
                    Calibration1.CalibrationParam = calibrationParam;
                    Calibration1.DataContext = calibrationParam;
                }
            };
            ComboxCalibrationTemplate.SelectedIndex = 0;
        }





        private void SendDemo_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxCameraType.SelectedItem is KeyValuePair<CameraType, string> KeyValue && KeyValue.Key is CameraType cameraType)
            {
                MQTTCamera.Init(cameraType);
            }
        }
        private void SendDemo1_Click(object sender, RoutedEventArgs e)
        {
            MQTTCamera.Calibration(Calibration1.CalibrationParam);
        }

        private void SendDemo2_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxCameraTakeImageMode.SelectedItem is KeyValuePair<TakeImageMode, string> KeyValue && KeyValue.Key is TakeImageMode takeImageMode)
            {
                MQTTCamera.Open(ComboxCameraID.Text.ToString(), takeImageMode,int.Parse(ComboxCameraImageBpp.Text));
            }


        }

        private void SendDemo3_Click(object sender, RoutedEventArgs e)
        {
            MQTTCamera.GetData(SliderexpTime.Value, SliderGain.Value);
        }

        private void SendDemo4_Click(object sender, RoutedEventArgs e)
        {
            MQTTCamera.Close();
        }


        private void MenuItem_Click_2(object sender, RoutedEventArgs e)
        {
            WindowTemplate windowTemplate = new WindowTemplate(WindowTemplateType.AoiParam);
            windowTemplate.Title ="AOI参数设置";
            TemplateAbb(windowTemplate, TemplateControl.GetInstance().AoiParams);
        }

        private void MenuItem_Click_3(object sender, RoutedEventArgs e)
        {
            TemplateControl templateControl = TemplateControl.GetInstance();
            Calibration calibration = new Calibration(templateControl.CalibrationParams[0].Value);
            WindowTemplate windowTemplate = new WindowTemplate(WindowTemplateType.Calibration, calibration);
            windowTemplate.Title ="校正参数设置";
            TemplateAbb(windowTemplate, TemplateControl.GetInstance().CalibrationParams);

        }

        private void MenuItem_Click_4(object sender, RoutedEventArgs e)
        {
            TemplateControl templateControl = TemplateControl.GetInstance();
            PG calibration = new PG(templateControl.PGParams[0].Value);
            WindowTemplate windowTemplate = new WindowTemplate(WindowTemplateType.PGParam, calibration);
            windowTemplate.Title = "PG通讯设置";
            TemplateAbb(windowTemplate, TemplateControl.GetInstance().PGParams);
        }
        private void MenuItem_Click_5(object sender, RoutedEventArgs e)
        {
            WindowTemplate windowTemplate = new WindowTemplate(WindowTemplateType.LedReuslt);
            windowTemplate.Title = "数据判断模板设置";
            TemplateAbb(windowTemplate, TemplateControl.GetInstance().LedReusltParams);
        }
        private void MenuItem_Click_6(object sender, RoutedEventArgs e)
        {
            WindowTemplate windowTemplate = new WindowTemplate(WindowTemplateType.SxParm);
            windowTemplate.Title = "源表模板设置";
            TemplateAbb(windowTemplate, TemplateControl.GetInstance().SxParms);
        }

        private void TemplateAbb<T>(WindowTemplate windowTemplate, ObservableCollection<KeyValuePair<string, T>>  keyValuePairs)
        {
            windowTemplate.Owner = this;
            int id = 1;
            foreach (var item in keyValuePairs)
            {
                ListConfig listConfig = new ListConfig();
                listConfig.ID = id++;
                listConfig.Name = item.Key;
                listConfig.Value = item.Value;
                windowTemplate.ListConfigs.Add(listConfig);
            }
            windowTemplate.Show();
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NativeMethods.Keyboard.PressKey(0x09);
            }
        }

        private void MenuItem_Click_7(object sender, RoutedEventArgs e)
        {
            WindowORM windowORM = new WindowORM();
            windowORM.Show();
        }



        private void FilterWheelSetPort_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxFilterWheelChannel.SelectedIndex > -1)
            {
                MQTTCamera.FilterWheelSetPort(ComboxFilterWheelChannel.SelectedIndex + 0x30);
            }
        }

        private void FilterWheelReset_Click(object sender, RoutedEventArgs e)
        {
            MQTTCamera.FilterWheelSetPort(0x30);
            ComboxFilterWheelChannel.SelectedIndex = 0;
        }


        private void StackPanelFilterWheel_Initialized(object sender, EventArgs e)
        {
            for (int i = 0; i < 10; i++)
                ComboxFilterWheelChannel.Items.Add(new ComboBoxItem() { Content = i });
            ComboxFilterWheelChannel.SelectedIndex = 0;
        }

        private void MenuItem_Click8(object sender, RoutedEventArgs e)
        {
            WindowFourColorCalibration windowFourColorCalibration = new WindowFourColorCalibration();
            windowFourColorCalibration.Owner = this;
            windowFourColorCalibration.Show();
        }
    }




    public class ImageInfo : ViewModelBase
    {
        public int X { get => _X; set { _X = value; NotifyPropertyChanged(); } }
        private int _X;

        public int Y { get => _Y; set { _Y = value; NotifyPropertyChanged(); } }
        private int _Y;

        public double X1 { get => _X1 ; set { _X1 = value; NotifyPropertyChanged(); } }
        private double _X1;

        public double Y1 { get => _Y1; set { _Y1 = value; NotifyPropertyChanged(); } }
        private double _Y1;

        public int R { get => _R; set { _R = value; NotifyPropertyChanged(); } }
        private int _R;

        public int G { get => _G; set { _G = value; NotifyPropertyChanged(); } }
        private int _G;

        public int B { get => _B; set {  _B = value; NotifyPropertyChanged(); } }
        private int _B;

        public string Hex { get => _Hex; set { _Hex = value; NotifyPropertyChanged(); } }
        private string _Hex;

        private SolidColorBrush _Color;
        public SolidColorBrush Color { get => _Color; set { _Color = value; NotifyPropertyChanged(); } }    
    }

}
