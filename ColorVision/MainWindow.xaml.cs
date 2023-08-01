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
using ColorVision.Video;
using ScottPlot.Drawing.Colormaps;
using ScottPlot.Styles;
using System.Drawing.Imaging;
using HandyControl.Expression.Shapes;
using Microsoft.Win32;
using log4net;
using System.Security.RightsManagement;
using ColorVision.MQTT.Control;

namespace ColorVision
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : Window
    {
        public ToolBarTop ToolBarTop { get; set; }
        public GlobalSetting GlobalSetting { get; set; }
        public SoftwareSetting SoftwareSetting { get
            {
                if (GlobalSetting.SoftwareConfig.SoftwareSetting == null)
                    GlobalSetting.SoftwareConfig.SoftwareSetting = new SoftwareSetting();
                return GlobalSetting.SoftwareConfig.SoftwareSetting;
            }
        }
        public MainWindow()
        {
            GlobalSetting = GlobalSetting.GetInstance();
            MQTTConfig mQTTConfig = GlobalSetting.SoftwareConfig.MQTTConfig;
            FlowEngineLib.MQTTHelper.SetDefaultCfg(mQTTConfig.Host, mQTTConfig.Port, mQTTConfig.UserName, mQTTConfig.UserPwd, false, null);
            this.loader = new FlowEngineLib.STNodeLoader("FlowEngineLib.dll");

            InitializeComponent();
            this.Closed += (s, e) => {

                SoftwareSetting.Top = this.Top;
                SoftwareSetting.Left = this.Left;
                SoftwareSetting.Height = this.Height;
                SoftwareSetting.Width = this.Width;
                SoftwareSetting.WindowState = (int)this.WindowState;
            };
            if (SoftwareSetting.IsRestoreWindow && SoftwareSetting.Height != 0&& SoftwareSetting.Width != 0)
            {
                this.Top = SoftwareSetting.Top;
                this.Left = SoftwareSetting.Left;
                this.Height = SoftwareSetting.Height;
                this.Width = SoftwareSetting.Width;
                this.WindowState = (WindowState)SoftwareSetting.WindowState;
            }
        }

        private async void Window_Initialized(object sender, EventArgs e)
        {
            if (WindowConfig.IsExist)
            {
                if (WindowConfig.Icon == null)
                {
                    if (!ThemeManager.AppsUseLightTheme() || !ThemeManager.SystemUsesLightTheme())
                    {
                        this.Icon = new BitmapImage(new Uri("pack://application:,,,/ColorVision;component/Image/ColorVision1.ico"));
                    }
                    SystemEvents.UserPreferenceChanged += (s, e) =>
                    {
                        if (!ThemeManager.AppsUseLightTheme() || !ThemeManager.SystemUsesLightTheme())
                        {
                            this.Icon = new BitmapImage(new Uri("pack://application:,,,/ColorVision;component/Image/ColorVision1.ico"));
                        }
                        else
                        {
                            this.Icon = new BitmapImage(new Uri("pack://application:,,,/ColorVision;component/Image/ColorVision.ico"));
                        }
                    };
                    SystemParameters.StaticPropertyChanged += (s, e) =>
                    {
                        if (!ThemeManager.AppsUseLightTheme() || !ThemeManager.SystemUsesLightTheme())
                        {
                            this.Icon = new BitmapImage(new Uri("pack://application:,,,/ColorVision;component/Image/ColorVision1.ico"));
                        }
                        else
                        {
                            this.Icon = new BitmapImage(new Uri("pack://application:,,,/ColorVision;component/Image/ColorVision.ico"));
                        }
                    };
                }
                else
                {
                    this.Icon = WindowConfig.Icon;
                }
                this.Title = WindowConfig.Title ?? this.Title;
            }
            else
            {

            }

            Application.Current.MainWindow = this;

            TemplateControl = TemplateControl.GetInstance();
            await Task.Delay(30);
            ToolBar1.Visibility = Visibility.Collapsed;

            ToolBarTop = new ToolBarTop(Zoombox1, ImageShow);
            ToolBar1.DataContext = ToolBarTop;
            ListView1.ItemsSource = DrawingVisualLists;
            StatusBarGrid.DataContext = GlobalSetting.GetInstance();
            SoftwareConfig SoftwareConfig = GlobalSetting.GetInstance().SoftwareConfig;
            MenuStatusBar.DataContext = SoftwareConfig;
            SiderBarGrid.DataContext = SoftwareConfig;

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
            DrawingVisualLists.CollectionChanged += (s, e) =>
            {
                if (DrawingVisualLists.Count == 0)
                {
                    ImageGroupGrid.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ImageGroupGrid.Visibility = Visibility.Visible;
                }
            };

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
                        ToolBarTop.DrawImage(actPoint, bitPoint, new ImageInfo
                        {
                            X = point.X.ToInt32(),
                            Y = point.Y.ToInt32(),
                            X1 = point.X,
                            Y1 = point.Y,

                            R = color.R,
                            G = color.G,
                            B = color.B,
                            Color = new SolidColorBrush(color),
                            Hex = color.ToHex()
                        });
                    }

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
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
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

            loader.Load(ofd.FileName);

            flowControl = new FlowControl(MQTTControl.GetInstance(), loader.GetStartNodeName());
        }
        Window window;

        private void FlowControl_FlowCompleted(object? sender, EventArgs e)
        {
            flowControl.FlowCompleted -= FlowControl_FlowCompleted;
            //MessageBox.Show("流程执行完成");
            window.Close();
        }
        bool CameraOpen;

        private void Button4_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                CameraVideoControl control = CameraVideoControl.GetInstance();
                if (!CameraOpen)
                {
                    button.Content = "正在获取推流";  
                    control.Open();
                    control.CameraVideoFrameReceived += (bmp) =>
                    {
                        button.Content = "关闭视频";
                        if (ImageShow.Source is WriteableBitmap bitmap)
                        {
                            ImageUtil.BitmapCopyToWriteableBitmap(bmp, bitmap, new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height), 0, 0, bmp.PixelFormat);
                        }
                        else
                        {
                            WriteableBitmap writeableBitmap = ImageUtil.BitmapToWriteableBitmap(bmp);
                            ImageShow.Source = writeableBitmap;
                        }
                    };
                }
                else
                {
                    button.Content = "启用视频模式";
                    control.Close();
                }
                CameraOpen = !CameraOpen;
            }

        }


        private void MenuItem11_Click(object sender, RoutedEventArgs e)
        {
            new HeartbeatWindow() { Owner = this }.Show();
        }

        private void MenuItem12_Click(object sender, RoutedEventArgs e)
        {
            new MQTTList() { Owner = this }.Show();
        }

        private void Button_FlowRun_Click(object sender, RoutedEventArgs e)
        {
            if (FlowTemplate.SelectedValue is FlowParam flowParam)
            {
                string startNode = loader.GetStartNodeName();
                if (!string.IsNullOrWhiteSpace(startNode))
                {
                    flowControl = new FlowControl(MQTTControl.GetInstance(), startNode);

                    window = new Window() { Width = 400, Height = 400, Title = "流程返回信息", Owner = this, ResizeMode = ResizeMode.NoResize, WindowStyle = WindowStyle.None, WindowStartupLocation = WindowStartupLocation.CenterOwner };
                    TextBox textBox = new TextBox() { IsReadOnly = true, Background = Brushes.Black, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

                    Grid grid = new Grid();
                    grid.Children.Add(textBox);

                    grid.Children.Add(new Controls.ProgressRing() { Margin = new Thickness(100, 100, 100, 100) });

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
                    string sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
                    flowControl.Start(sn);
                    window.Show();
                }
                else
                {
                    MessageBox.Show("流程模板为空，不能运行！！！");
                }
            }
        }

        private void StackPanelFlow_Initialized(object sender, EventArgs e)
        {
            FlowTemplate.ItemsSource = TemplateControl.GetInstance().FlowParams;

            FlowTemplate.SelectionChanged += (s, e) =>
            {
                if (FlowTemplate.SelectedValue is FlowParam flowParam)
                {
                    string fileName = GlobalSetting.GetInstance().SoftwareConfig.SolutionConfig.GetFullFileName(flowParam.FileName?? string.Empty);
                    if (File.Exists(fileName))
                    {
                        loader.Load(fileName);
                    }
                }
            };
            FlowTemplate.SelectedIndex = 0;
        }

        private void StackPanelMQTTCameras_Initialized(object sender, EventArgs e)
        {
            if (sender is StackPanel stackPanel)
            {
                
                MQTTCameraControl1 mQTTCameraControl = new MQTTCameraControl1(MQTTManager.GetInstance().MQTTCameras[0].Value);
                stackPanel.Children.Add(mQTTCameraControl);
                MQTTCameraControl mQTTCameraControl1 = new MQTTCameraControl(MQTTManager.GetInstance().MQTTCameras[1].Value);
                stackPanel.Children.Add(mQTTCameraControl1);
            }
        }
        private void SendDemo1_Click(object sender, RoutedEventArgs e)
        {
            //MQTTCamera.Calibration(Calibration1.CalibrationParam);
        }

        private void StackPanelMQTTPGs_Initialized(object sender, EventArgs e)
        {
            if (sender is StackPanel stackPanel)
            {
                MQTTPGControl Control = new MQTTPGControl(MQTTManager.GetInstance().MQTTPGs[0].Value);
                stackPanel.Children.Add(Control);
            }
          
        }

        private void StackPanelMQTTSpectrums_Initialized(object sender, EventArgs e)
        {
            if (sender is StackPanel stackPanel)
            {
                MQTTSpectrumControl Control = new MQTTSpectrumControl(MQTTManager.GetInstance().MQTTSpectrums[0].Value);
                stackPanel.Children.Add(Control);
            }
        }

        private void StackPanelMQTTVIs_Initialized(object sender, EventArgs e)
        {
            if (sender is StackPanel stackPanel)
            {
                MQTTVISourceControl Control = new MQTTVISourceControl(MQTTManager.GetInstance().MQTTVISources[0].Value);
                stackPanel.Children.Add(Control);
            }
            
        }

        private void MenuItem13_Click(object sender, RoutedEventArgs e)
        {
            new ServiceManager() { Owner = this }.Show();
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
