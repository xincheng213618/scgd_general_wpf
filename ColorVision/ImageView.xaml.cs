using ColorVision.Extension;
using ColorVision.MVVM;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision
{
    /// <summary>
    /// ImageView.xaml 的交互逻辑
    /// </summary>
    public partial class ImageView : UserControl, IView
    {
        public ToolBarTop ToolBarTop { get; set; }

        public View View { get; set; }

        public ImageView()
        {
            InitializeComponent();
            View = new View();
        }

        public ObservableCollection<IDrawingVisual> DrawingVisualLists { get; set; } = new ObservableCollection<IDrawingVisual>();

        private DrawingVisual ImageRuler = new DrawingVisual();
        private DrawingVisual DrawingVisualGrid = new DrawingVisual();

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ContextMenu ContextMenu  = new ContextMenu();

            MenuItem menuItem = new MenuItem() { Header = "设为主窗口" };
            menuItem.Click += (s, e) =>
            {
                ViewGridManager.GetInstance().SetOneView(this);
            };
            ContextMenu.Items.Add(menuItem);

            MenuItem menuItem1 = new MenuItem() { Header = "展示全部窗口" };
            menuItem1.Click += (s, e) =>
            {
                ViewGridManager.GetInstance().SetViewNum(-1);
            };
            ContextMenu.Items.Add(menuItem1);
            this.ContextMenu = ContextMenu;
            ToolBar1.Visibility = Visibility.Collapsed;

            ToolBarTop = new ToolBarTop(Zoombox1, ImageShow);
            ToolBar1.DataContext = ToolBarTop;
            ListView1.ItemsSource = DrawingVisualLists;


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
            if (e.Key == Key.Enter && e.Key == Key.Escape)
            {
                if (DrawingVisualPolygonCache != null)
                {
                    DrawingVisualPolygonCache.Attribute.Points.Add(MouseDownP);
                    DrawingVisualPolygonCache.IsDrawing = false;
                    DrawingVisualPolygonCache.Render();
                    DrawingVisualPolygonCache.AutoAttributeChanged = true;
                }
                this.KeyDown -= DrawingVisualPolygonKeyDown;
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
                    else if (SelectDRectangle != null)
                    {
                        var OldRect = SelectDRectangle.Attribute.Rect;
                        SelectDRectangle.Attribute.Rect = new Rect(OldRect.X + point.X - LastMouseMove.X, OldRect.Y + point.Y - LastMouseMove.Y, OldRect.Width, OldRect.Height);

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



        public class WindowStatus
        {
            public object Root { get; set; }
            public Grid Parent { get; set; }

            public WindowStyle WindowStyle { get; set; }

            public WindowState WindowState { get; set; }

            public ResizeMode ResizeMode { get; set; }


        }
        private WindowStatus OldWindowStatus { get; set; }


        private void Button8_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggleButton)
            {
                var mainWindow = Application.Current.MainWindow;

                if (toggleButton.IsChecked == true)
                {
                    if (this.VisualParent is Grid p)
                    {
                        OldWindowStatus = new WindowStatus();
                        OldWindowStatus.Parent = p;
                        OldWindowStatus.WindowState = mainWindow.WindowState;
                        OldWindowStatus.WindowStyle = mainWindow.WindowStyle;
                        OldWindowStatus.ResizeMode = mainWindow.ResizeMode;
                        OldWindowStatus.Root = mainWindow.Content;

                        mainWindow.WindowStyle = WindowStyle.None;
                        mainWindow.WindowState = WindowState.Maximized;

                        OldWindowStatus.Parent.Children.Remove(this);
                        mainWindow.Content = this;
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {

                    mainWindow.WindowStyle = OldWindowStatus.WindowStyle;
                    mainWindow.WindowState = OldWindowStatus.WindowState;
                    mainWindow.ResizeMode = OldWindowStatus.ResizeMode;

                    mainWindow.Content = OldWindowStatus.Root;
                    OldWindowStatus.Parent.Children.Add(this);
                }

            }
        }

        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        private static extern void RtlMoveMemory(IntPtr Destination, IntPtr Source, uint Length);

        [DllImport("OpenCVHelper.dll")]
        private static extern void ReadCVFile(string FullPath);

        [DllImport("OpenCVHelper.dll")]
        public unsafe static extern void SetInitialFrame(nint pRoutineHandler);



        [UnmanagedCallersOnly(CallConvs = new System.Type[] { typeof(CallConvCdecl) })]
        [SuppressGCTransition]
        private static int InitialFrame(IntPtr buff, int rows, int cols, int type)
        {
            PixelFormat format = type switch
            {
                1 => PixelFormats.Gray8,
                3 => PixelFormats.Bgr24,
                4 => PixelFormats.Bgr32,
                _ => PixelFormats.Default,
            };
            if (rows == 0) { return 2; }

            Application.Current.Dispatcher.Invoke(delegate
            {
                WriteableBitmap writeableBitmap = new WriteableBitmap(cols, rows, 96.0, 96.0, format, null);
                RtlMoveMemory(writeableBitmap.BackBuffer, buff, (uint)(cols * rows * type));
                writeableBitmap.Lock();
                writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
                writeableBitmap.Unlock();
                if (ViewGridManager.GetInstance().Views[1] is ImageView view)
                {
                    view.ImageShow.Source = writeableBitmap;
                }
            });
            return 0;
        }

        public unsafe void OpenCVImage(string? filePath)
        {
            SetInitialFrame((nint)(delegate* unmanaged[Cdecl]<IntPtr, int, int, int,int >)(&InitialFrame));

            if (filePath != null && File.Exists(filePath))
            {

                ReadCVFile(filePath);

                //BitmapImage bitmapImage = new BitmapImage(new Uri(filePath));
                //ImageShow.Source = bitmapImage;
                //DrawGridImage(DrawingVisualGrid, bitmapImage);
                Zoombox1.ZoomUniform();
                ToolBar1.Visibility = Visibility.Visible;

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



        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
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




    }


    public class ImageInfo : ViewModelBase
    {
        public int X { get; set; }
        public int Y { get; set; }
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public int R { get; set; }
        public int G { get; set; }
        public int B { get; set; }
        public string Hex { get; set; }
        public SolidColorBrush Color { get; set; }
    }
}
