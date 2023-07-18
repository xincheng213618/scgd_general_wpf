using ColorVision.Extension;
using ColorVision.MQTT;
using ColorVision.MVVM;
using ColorVision.MySql;
using ColorVision.Solution;
using ColorVision.SettingUp;
using ColorVision.Template;
using Microsoft.VisualBasic.Logging;
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
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using ColorVision.Theme;
using ColorVision.Util;
using System.Windows.Forms.Integration;
using OpenCvSharp.Flann;

namespace ColorVision
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : Window
    {
        public ImageInfo ImageInfo { get; set; } = new ImageInfo();

        public ToolBarTop ToolBarTop { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            this.Closed += (s, e) => Environment.Exit(0);
        }

        private async void Window_Initialized(object sender, EventArgs e)
        {
            bool isHighContrast = SystemParameters.HighContrast;

            if (isHighContrast||!ThemeManager.AppsUseLightTheme()||!ThemeManager.SystemUsesLightTheme())
            {
                this.Icon = new BitmapImage(new Uri("pack://application:,,,/ColorVision;component/Image/ColorVision1.ico"));
            }


            if (WindowConfig.IsExist)
            {
                this.Icon = WindowConfig.Icon ?? this.Icon;
                this.Title = WindowConfig.Title ?? this.Title;
            }

            Application.Current.MainWindow = this;
            TemplateControl = TemplateControl.GetInstance();

            await Task.Delay(30);
            ToolBar1.Visibility = Visibility.Collapsed;

            ToolBarTop = new ToolBarTop(Zoombox1, ImageShow);
            ToolBar1.DataContext = ToolBarTop;
            ListView1.ItemsSource = DrawingVisualLists;
            SoftwareConfig SoftwareConfig = GlobalSetting.GetInstance().SoftwareConfig;

            StatusBarItem1.DataContext = GlobalSetting.GetInstance().PerformanceControl;
            StatusBarItem2.DataContext = GlobalSetting.GetInstance().PerformanceControl;

            StatusBarItem3.DataContext = SoftwareConfig.ProjectConfig;
            StatusBarMqtt.DataContext = SoftwareConfig.MQTTControl;
            StatusBarMysql.DataContext = SoftwareConfig.MySqlControl;

            StatusBarGrid.DataContext = SoftwareConfig;
            MenuStatusBar.DataContext = SoftwareConfig;
            SiderBarGrid.DataContext = SoftwareConfig;
            
        }




        private DrawingVisual ImageRuler = new DrawingVisual();
        private void Button_Click(object sender, RoutedEventArgs e)
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
            if (filePath != null && File.Exists(filePath))
            {
                BitmapImage bitmapImage = new BitmapImage(new Uri(filePath));

                ImageShow.Source = bitmapImage;
                DrawGridImage(DrawingVisualGrid, bitmapImage);
                Zoombox1.ZoomUniform();
                ToolBar1.Visibility = Visibility.Visible;

                ImageShow.VisualsAdd += (s, e) =>
                {
                    if (s is IDrawingVisual visual && !DrawingVisualLists.Contains(visual) && s is Visual visual1)
                    {
                        DrawingVisualLists.Add(visual);
                        visual.GetAttribute().PropertyChanged += (s1, e1) =>
                        {
                            if (e1.PropertyName == "IsShow")
                            {
                                ListView1.ScrollIntoView(visual);
                                ListView1.SelectedIndex = DrawingVisualLists.IndexOf(visual);
                                if (visual.GetAttribute().IsShow == true)
                                {
                                    if (!ImageShow.ContainsVisual(visual1))
                                    {
                                        ImageShow.AddVisual(visual1);
                                    }
                                }
                                else
                                {
                                    if (ImageShow.ContainsVisual(visual1))
                                    {
                                        ImageShow.RemoveVisual(visual1);
                                    }
                                }
                            }
                        };

                    }
                };

                //如果是不显示
                ImageShow.VisualsRemove += (s, e) =>
                {
                    if (s is IDrawingVisual visual)
                    {
                        if (visual.GetAttribute().IsShow)
                            DrawingVisualLists.Remove(visual);
                    }
                };
            }
        }

        public ObservableCollection<IDrawingVisual> DrawingVisualLists { get; set; } = new ObservableCollection<IDrawingVisual>();


        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < 50; i++)
            {
                for (int j = 0; j < 50; j++)
                {
                    DrawingVisualCircle drawingVisualCircle = new DrawingVisualCircle();
                    drawingVisualCircle.Attribute.Center = new Point(i * 50, j * 50);
                    drawingVisualCircle.Attribute.Radius = 20;
                    drawingVisualCircle.Attribute.Brush = Brushes.Transparent;
                    drawingVisualCircle.Attribute.Pen = new Pen(Brushes.Red, 10);
                    drawingVisualCircle.Render();
                    ImageShow.AddVisual(drawingVisualCircle);
                }
            }
            PropertyGrid2.SelectedObject = DrawingVisualLists[0].GetAttribute();
            DrawingVisualLists[0].GetAttribute().PropertyChanged += (s, e) =>
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
                if (!ImageShow.ContainsVisual(DrawingVisualGrid) && toggleButton.IsChecked == true)
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
                FormattedText formattedText = new FormattedText((1 / Zoombox1.ContentMatrix.M11 * bitmapImage.PixelWidth / 100).ToString("F2") + "px", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(formattedText, new Point(100, 30));


                double X = 1 / Zoombox1.ContentMatrix.M11 * bitmapImage.PixelWidth / 100;
                double result = X < 10 ? 5 : X < 20 ? 10 : X < 50 ? 20 : X < 100 ? 50 : (X < 200 ? 100 : (X < 500 ? 200 : (X < 1000 ? 500 : (X < 2000 ? 1000 : 2000))));

                dc.DrawLine(new Pen(Brushes.Red, 10), new Point(100, 100), new Point(100 + 100 * result / X, 100));
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

        private bool IsMouseDown;
        private Point MouseDownP;
        private DrawingVisualCircle? SelectDCircle;
        private DrawingVisualRectangle? SelectDRectangle;

        private DrawingVisualCircle DrawCircleCache;
        private DrawingVisualRectangle DrawingRectangleCache;
        private DrawingVisualPolygon DrawingVisualPolygonCache;


        private void ImageShow_Initialized(object sender, EventArgs e)
        {
            ImageShow.ContextMenuOpening += MainWindow_ContextMenuOpening;
        }

        private void MainWindow_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var Point = Mouse.GetPosition(ImageShow);
            var DrawingVisual = ImageShow.GetVisual(Point);

            if (DrawingVisual != null && DrawingVisual is IDrawingVisual drawing)
            {
                var ContextMenu = new ContextMenu();

                MenuItem menuItem = new MenuItem() { Header = "隐藏(_H)" };
                menuItem.Click += (s, e) =>
                {
                    drawing.GetAttribute().IsShow = false;
                };
                MenuItem menuIte2 = new MenuItem() { Header = "删除(_D)" };

                menuIte2.Click += (s, e) =>
                {
                    ImageShow.RemoveVisual(DrawingVisual);
                    PropertyGrid2.SelectedObject = null;
                };
                ContextMenu.Items.Add(menuItem);
                ContextMenu.Items.Add(menuIte2);
                ImageShow.ContextMenu = ContextMenu;
            }
            else
            {
                ImageShow.ContextMenu = null;
            }

        }
        private void ListView1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                if (sender is ListView listView && listView.SelectedIndex > -1 && DrawingVisualLists[ListView1.SelectedIndex] is Visual visual)
                {
                    ImageShow.RemoveVisual(visual);
                    PropertyGrid2.SelectedObject = null;
                }
            }
        }

        private void MenuItem_DrawingVisual_Delete(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is Visual visual)
            {
                PropertyGrid2.SelectedObject = null;
                ImageShow.RemoveVisual(visual);
            }
        }


        private void ImageShow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas && !Keyboard.Modifiers.HasFlag(Zoombox1.ActivateOn))
            {
                MouseDownP = e.GetPosition(drawCanvas);
                IsMouseDown = true;
                drawCanvas.CaptureMouse();

                if (ToolBarTop.EraseVisual)
                {
                    DrawSelectRect(SelectRect, new Rect(MouseDownP, MouseDownP)); ;
                    drawCanvas.AddVisual(SelectRect);
                }
                else if (ToolBarTop.DrawCircle)
                {
                    DrawCircleCache = new DrawingVisualCircle() { AutoAttributeChanged = false };
                    DrawCircleCache.Attribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                    DrawCircleCache.Attribute.Center = MouseDownP;
                    drawCanvas.AddVisual(DrawCircleCache);
                }
                else if (ToolBarTop.DrawRect)
                {
                    DrawingRectangleCache = new DrawingVisualRectangle() { AutoAttributeChanged = false };
                    DrawingRectangleCache.Attribute.Rect = new Rect(MouseDownP, new Point(MouseDownP.X + 30, MouseDownP.Y + 30));
                    DrawingRectangleCache.Attribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);

                    drawCanvas.AddVisual(DrawingRectangleCache);
                }
                else if (ToolBarTop.DrawPolygon)
                {
                    if (DrawingVisualPolygonCache == null)
                    {
                        DrawingVisualPolygonCache = new DrawingVisualPolygon() { AutoAttributeChanged = false };
                        DrawingVisualPolygonCache.Attribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                        DrawingVisualPolygonCache.Attribute.Points.Add(MouseDownP);
                        drawCanvas.AddVisual(DrawingVisualPolygonCache);
                    }
                    else
                    {
                        DrawingVisualPolygonCache.Attribute.Points.Add(MouseDownP);
                    }
                    this.KeyDown += DrawingVisualPolygonKeyDown;
                }
                else
                {
                    if (drawCanvas.GetVisual(MouseDownP) is IDrawingVisual drawingVisual)
                    {
                        if (PropertyGrid2.SelectedObject is DrawAttributeBase viewModelBase)
                        {
                            viewModelBase.PropertyChanged -= (s, e) =>
                            {
                                PropertyGrid2.Refresh();
                            };
                        }
                        PropertyGrid2.SelectedObject = drawingVisual.GetAttribute();
                        drawingVisual.GetAttribute().PropertyChanged += (s, e) =>
                        {
                            PropertyGrid2.Refresh();
                        };

                        ListView1.ScrollIntoView(drawingVisual);
                        ListView1.SelectedIndex = DrawingVisualLists.IndexOf(drawingVisual);

                        if (ToolBarTop.Activate == true)
                        {
                            if (drawingVisual is DrawingVisualRectangle Rectangle)
                            {
                                SelectDRectangle = Rectangle;
                            }
                            else if (drawingVisual is DrawingVisualCircle Circl)
                            {
                                SelectDCircle = Circl;
                            }

                        }
                    }

                }

            }
        }


        public void DrawingVisualPolygonKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter&& e.Key == Key.Escape)
            {
                if (DrawingVisualPolygonCache != null)
                {
                    DrawingVisualPolygonCache.Attribute.Points.Add(MouseDownP);
                    DrawingVisualPolygonCache.IsDrawing = false;
                    DrawingVisualPolygonCache.Render();
                    DrawingVisualPolygonCache.AutoAttributeChanged = true;
                }
                this.KeyDown-= DrawingVisualPolygonKeyDown;
            }
        }

        Point LastMouseMove;
        private void ImageShow_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas && (Zoombox1.ActivateOn == ModifierKeys.None || !Keyboard.Modifiers.HasFlag(Zoombox1.ActivateOn)))
            {
                var point = e.GetPosition(drawCanvas);

                var controlWidth = drawCanvas.ActualWidth;
                var controlHeight = drawCanvas.ActualHeight;

                if (IsMouseDown)
                {
                    if (ToolBarTop.EraseVisual)
                    {
                        DrawSelectRect(SelectRect, new Rect(MouseDownP, point)); ;
                    }
                    else if (ToolBarTop.DrawCircle)
                    {
                        double Radius = Math.Sqrt((Math.Pow(point.X - MouseDownP.X, 2) + Math.Pow(point.Y - MouseDownP.Y, 2)));
                        DrawCircleCache.Attribute.Radius = Radius;
                        DrawCircleCache.Render();
                    }
                    else if (ToolBarTop.DrawRect)
                    {
                        DrawingRectangleCache.Attribute.Rect = new Rect(MouseDownP, point);
                        DrawingRectangleCache.Render();
                    }
                    else if (ToolBarTop.DrawPolygon)
                    {
                        DrawingVisualPolygonCache.Attribute.Points[^1] = point;
                        DrawingVisualPolygonCache.Render();
                    }
                    else if (SelectDCircle != null)
                    {
                        SelectDCircle.Attribute.Center += point - LastMouseMove;
                    } 
                    else if (SelectDRectangle !=null)
                    {
                        var OldRect = SelectDRectangle.Attribute.Rect;
                        SelectDRectangle.Attribute.Rect = new Rect(OldRect.X + point.X - LastMouseMove.X, OldRect.Y +  point.Y - LastMouseMove.Y, OldRect.Width,OldRect.Height);

                    }
                }

                if (ToolBarTop.Move && drawCanvas.Source is BitmapImage bitmapImage)
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
                LastMouseMove = point;
            }
        }
        private void ImageShow_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas && !Keyboard.Modifiers.HasFlag(Zoombox1.ActivateOn))
            {
                IsMouseDown = false;
                var MouseUpP = e.GetPosition(drawCanvas);
                if (ToolBarTop.EraseVisual)
                {
                    drawCanvas.RemoveVisual(drawCanvas.GetVisual(MouseDownP));
                    drawCanvas.RemoveVisual(drawCanvas.GetVisual(MouseUpP));
                    foreach (var item in drawCanvas.GetVisuals(new RectangleGeometry(new Rect(MouseDownP, MouseUpP))))
                    {
                        drawCanvas.RemoveVisual(item);
                    }
                    drawCanvas.RemoveVisual(SelectRect);
                }
                else if (ToolBarTop.DrawCircle)
                {
                    if (DrawCircleCache.Attribute.Radius == 30)
                        DrawCircleCache.Render();

                    if (PropertyGrid2.SelectedObject is ViewModelBase viewModelBase)
                    {
                        viewModelBase.PropertyChanged -= (s, e) =>
                        {
                            PropertyGrid2.Refresh();
                        };
                    }

                    PropertyGrid2.SelectedObject = DrawCircleCache.Attribute;
                    DrawCircleCache.Attribute.PropertyChanged += (s, e) =>
                    {
                        PropertyGrid2.Refresh();
                    };
                    DrawCircleCache.AutoAttributeChanged = true;

                    ListView1.ScrollIntoView(DrawCircleCache);
                    ListView1.SelectedIndex = DrawingVisualLists.IndexOf(DrawCircleCache);

                }
                else if (ToolBarTop.DrawRect)
                {
                    if (DrawingRectangleCache.Attribute.Rect.Width == 30 && DrawingRectangleCache.Attribute.Rect.Height == 30)
                        DrawingRectangleCache.Render();

                    if (PropertyGrid2.SelectedObject is ViewModelBase viewModelBase)
                    {
                        viewModelBase.PropertyChanged -= (s, e) =>
                        {
                            PropertyGrid2.Refresh();
                        };
                    }
                    PropertyGrid2.SelectedObject = DrawingRectangleCache.Attribute;
                    DrawingRectangleCache.AutoAttributeChanged = true;

                    DrawingRectangleCache.Attribute.PropertyChanged += (s, e) =>
                    {
                        PropertyGrid2.Refresh();
                    };

                    ListView1.ScrollIntoView(DrawingRectangleCache);
                    ListView1.SelectedIndex = DrawingVisualLists.IndexOf(DrawingRectangleCache);

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



        private void Button7_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggleButton)
            {
                if (ToolBarTop.EraseVisual)
                {
                    ToggleButtonDrag.IsChecked = true;
                    Zoombox1.ActivateOn = toggleButton.IsChecked == true ? ModifierKeys.Control : ModifierKeys.None;
                }
            }
        }





        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.Tag is Visual visual && visual is IDrawingVisual iDdrawingVisual)
            {

            }
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            new MQTTLog() { Owner = this }.Show();
        }


        private void About_Click(object sender, RoutedEventArgs e)
        {
            new AboutMsgWindow() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
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
                if (toolBar.Template.FindName("OverflowGrid", toolBar) is FrameworkElement overflowGrid)
                    overflowGrid.Visibility = Visibility.Collapsed;
                if (toolBar.Template.FindName("MainPanelBorder", toolBar) is FrameworkElement mainPanelBorder)
                    mainPanelBorder.Margin = new Thickness(0);
            }
        }


        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1 && DrawingVisualLists[listView.SelectedIndex] is IDrawingVisual drawingVisual && DrawingVisualLists[listView.SelectedIndex] is Visual visual)
            {
                if (PropertyGrid2.SelectedObject is ViewModelBase viewModelBase)
                {
                    viewModelBase.PropertyChanged -= (s, e) =>
                    {
                        PropertyGrid2.Refresh();
                    };
                }

                PropertyGrid2.SelectedObject = drawingVisual.GetAttribute();
                drawingVisual.GetAttribute().PropertyChanged += (s, e) =>
                {
                    PropertyGrid2.Refresh();
                };
                ImageShow.TopVisual(visual);
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
            StackPanelImage.Visibility = Visibility.Collapsed;
            CameraCloseButton.Visibility = Visibility.Collapsed;
            CameraOpenButton.Visibility = Visibility.Collapsed;

            MQTTCamera.InitCameraSuccess += (s, e) =>
            {
                ComboxCameraID.ItemsSource = MQTTCamera.CameraID?.IDs;
                ComboxCameraID.SelectedIndex = 0;
                StackPanelOpen.Visibility = Visibility.Visible;
                StackPanelImage.Visibility = Visibility.Visible;
                CameraOpenButton.Visibility = Visibility.Visible;
                CamerInitButton.Content = "断开初始化";
            };
            MQTTCamera.OpenCameraSuccess += (s, e) =>
            {
                CameraCloseButton.Visibility = Visibility.Visible;
                CameraOpenButton.Visibility = Visibility.Collapsed;
            };
            MQTTCamera.CloseCameraSuccess += (s, e) =>
            {
                CameraCloseButton.Visibility = Visibility.Collapsed;
                CameraOpenButton.Visibility = Visibility.Visible;
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

        private void MQTTCamera_Init_Click(object sender, RoutedEventArgs e)
        {
            if(sender is Button button)
            {
                if (button.Content.ToString() == "初始化")
                {
                    if (ComboxCameraType.SelectedItem is KeyValuePair<CameraType, string> KeyValue && KeyValue.Key is CameraType cameraType)
                    {
                        MQTTCamera.Init(cameraType);
                    }
                }
                else
                {
                    MQTTCamera.Close();
                }
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
                MQTTCamera.Open(ComboxCameraID.Text.ToString(), takeImageMode, int.Parse(ComboxCameraImageBpp.Text));
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






        private void FilterWheelSetPort_Click(object sender, RoutedEventArgs e)
        {
            if (ComboxFilterWheelChannel.SelectedIndex > -1)
            {
                MQTTCamera.FilterWheelSetPort(0,ComboxFilterWheelChannel.SelectedIndex + 0x30,(int)MQTTCamera.CurrentCameraType);
            }
        }


        private void FilterWheelSet_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MQTTCamera.FilterWheelSetPort(ComboxFilterWheelChannel1.SelectedIndex + 1, ComboxFilterWheelChannel2.SelectedIndex + 1, (int)MQTTCamera.CurrentCameraType);
            }
            catch
            {

            }
        }

        private void FilterWheelReset_Click(object sender, RoutedEventArgs e)
        {
            MQTTCamera.FilterWheelSetPort(0,0x30,(int)MQTTCamera.CurrentCameraType);
            ComboxFilterWheelChannel.SelectedIndex = 0;
        }


        private void StackPanelFilterWheel_Initialized(object sender, EventArgs e)
        {
            for (int i = 0; i < 10; i++)
                ComboxFilterWheelChannel.Items.Add(new ComboBoxItem() { Content = i });
            ComboxFilterWheelChannel.SelectedIndex = 0;
        }





        private void Zoombox1_Initialized(object sender, EventArgs e)
        {

        }

        private void MenuStatusBar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                menuItem.IsChecked = !menuItem.IsChecked;
            }
        }
        private FlowEngineLib.STNodeLoader loader;

        private FlowControl flowControl;

        private void Button2_Click(object sender, RoutedEventArgs e)
        {

            MQTTConfig MQTTConfig = GlobalSetting.GetInstance().SoftwareConfig.MQTTConfig;
            string iPStr = MQTTConfig.Host;
            int port = MQTTConfig.Port;
            string uName = "";
            string uPwd = "";
            FlowEngineLib.MQTTHelper.SetDefaultCfg(iPStr, port, uName, uPwd, false, null);


            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            ofd.Filter = "*.stn|*.stn";
            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            loader = new FlowEngineLib.STNodeLoader("FlowEngineLib.dll");
            loader.Load(ofd.FileName);

            flowControl = new FlowControl(MQTTControl.GetInstance(), loader.GetStartNodeName());
        }
        Window window;
        private void Button3_Click(object sender, RoutedEventArgs e)
        {
            window = new Window() { Width = 400, Height = 400, Title = "流程返回信息", Owner = this,ResizeMode =ResizeMode.NoResize , WindowStyle =WindowStyle.None, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            TextBox textBox = new TextBox() { IsReadOnly = true,Background =Brushes.Black, Foreground =Brushes.White, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

            Grid grid = new Grid(); 
            grid.Children.Add(textBox);

            grid.Children.Add(new Controls.ProgressRing() { Margin =  new Thickness(100,100,100,100)});

            window.Content = grid;

            textBox.Text = "TTL:" + "0";
            flowControl.FlowData += (s, e) =>
            {
                if (s is FlowControlData msg)
                {
                    textBox.Text = "TTL:" + msg.Params.TTL.ToString();
                }
            };
            flowControl.FlowCompleted += FlowControl_FlowCompleted;
            flowControl.Start();
            window.Show();
        }

        private void FlowControl_FlowCompleted(object? sender, EventArgs e)
        {
            flowControl.FlowCompleted -= FlowControl_FlowCompleted;
            //MessageBox.Show("流程执行完成");
            window.Close();
        }
        private const string H264DllName = "openh264-2.3.1-win64.dll";
        private OpenH264Lib.Decoder decoder;
        private H264Reader h264Reader;
        private void Button4_Click(object sender, RoutedEventArgs e)
        {
            decoder = new OpenH264Lib.Decoder(H264DllName);
            h264Reader = new H264Reader();
            string locateIP = "0.0.0.0"; //本机IP
            int locatePort = 9002;   //发送端口

            //var windowsFormsHost1 = new WindowsFormsHost();
            //pictureBox = new System.Windows.Forms.PictureBox();
            //windowsFormsHost1.Child = pictureBox;
            //Window window = new Window() { Width=1280,Height=720};
            //window.Content = windowsFormsHost1;
            //window.Show();
            UDPClientRecv udpClient = new UDPClientRecv(locateIP, locatePort);
            udpClient.UDPMessageReceived += UdpClient_UDPMessageReceived;
        }
        System.Windows.Forms.PictureBox pictureBox;
        private void UdpClient_UDPMessageReceived(UdpStateEventArgs args)
        {
            if (args.buffer.Length > 0)
            {
                byte[] bytes = h264Reader.AddPacket(args.buffer);
                if (bytes != null)
                {
                    var bmp = decoder.Decode(bytes, bytes.Length);
                    if (bmp != null)
                    {

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            WriteableBitmap writeableBitmap = BitmapToWriteableBitmap(bmp);
                            ImageShow.Source = writeableBitmap;
                        });

                        //pictureBox.Image = (System.Drawing.Bitmap)bmp.Clone();
                        Console.WriteLine("Display BMP / {0}", args.buffer[3]);
                        bmp.Dispose();
                    }
                }
            }
        }


        public static WriteableBitmap BitmapToWriteableBitmap(System.Drawing.Bitmap src)
        {
            var wb = CreateCompatibleWriteableBitmap(src);
            System.Drawing.Imaging.PixelFormat format = src.PixelFormat;
            if (wb == null)
            {
                wb = new WriteableBitmap(src.Width, src.Height, 0, 0, System.Windows.Media.PixelFormats.Bgra32, null);
                format = System.Drawing.Imaging.PixelFormat.Format32bppArgb;
            }
            BitmapCopyToWriteableBitmap(src, wb, new System.Drawing.Rectangle(0, 0, src.Width, src.Height), 0, 0, format);
            return wb;
        }
        //创建尺寸和格式与Bitmap兼容的WriteableBitmap
        public static WriteableBitmap CreateCompatibleWriteableBitmap(System.Drawing.Bitmap src)
        {
            System.Windows.Media.PixelFormat format;
            switch (src.PixelFormat)
            {
                case System.Drawing.Imaging.PixelFormat.Format16bppRgb555:
                    format = System.Windows.Media.PixelFormats.Bgr555;
                    break;
                case System.Drawing.Imaging.PixelFormat.Format16bppRgb565:
                    format = System.Windows.Media.PixelFormats.Bgr565;
                    break;
                case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
                    format = System.Windows.Media.PixelFormats.Bgr24;
                    break;
                case System.Drawing.Imaging.PixelFormat.Format32bppRgb:
                    format = System.Windows.Media.PixelFormats.Bgr32;
                    break;
                case System.Drawing.Imaging.PixelFormat.Format32bppPArgb:
                    format = System.Windows.Media.PixelFormats.Pbgra32;
                    break;
                case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
                    format = System.Windows.Media.PixelFormats.Bgra32;
                    break;
                default:
                    return null;
            }
            return new WriteableBitmap(src.Width, src.Height, 0, 0, format, null);
        }
        //将Bitmap数据写入WriteableBitmap中
        public static void BitmapCopyToWriteableBitmap(System.Drawing.Bitmap src, WriteableBitmap dst, System.Drawing.Rectangle srcRect, int destinationX, int destinationY, System.Drawing.Imaging.PixelFormat srcPixelFormat)
        {
            var data = src.LockBits(new System.Drawing.Rectangle(new System.Drawing.Point(0, 0), src.Size), System.Drawing.Imaging.ImageLockMode.ReadOnly, srcPixelFormat);
            dst.WritePixels(new Int32Rect(srcRect.X, srcRect.Y, srcRect.Width, srcRect.Height), data.Scan0, data.Height * data.Stride, data.Stride, destinationX, destinationY);
            src.UnlockBits(data);
        }
    }




    public class ImageInfo : ViewModelBase
    {
        public int X { get => _X; set { _X = value; NotifyPropertyChanged(); } }
        private int _X;

        public int Y { get => _Y; set { _Y = value; NotifyPropertyChanged(); } }
        private int _Y;

        public double X1 { get => _X1; set { _X1 = value; NotifyPropertyChanged(); } }
        private double _X1;

        public double Y1 { get => _Y1; set { _Y1 = value; NotifyPropertyChanged(); } }
        private double _Y1;

        public int R { get => _R; set { _R = value; NotifyPropertyChanged(); } }
        private int _R;

        public int G { get => _G; set { _G = value; NotifyPropertyChanged(); } }
        private int _G;

        public int B { get => _B; set { _B = value; NotifyPropertyChanged(); } }
        private int _B;

        public string Hex { get => _Hex; set { _Hex = value; NotifyPropertyChanged(); } }
        private string _Hex;

        private SolidColorBrush _Color;
        public SolidColorBrush Color { get => _Color; set { _Color = value; NotifyPropertyChanged(); } }
    }

}
