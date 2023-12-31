﻿using ColorVision.Draw;
using ColorVision.Draw.Ruler;
using ColorVision.MVVM;
using ColorVision.Services.Algorithm;
using cvColorVision;
using FileServerPlugin;
using log4net;
using MQTTMessageLib.Algorithm;
using OpenCvSharp.WpfExtensions;
using Panuon.WPF;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Media
{
    /// <summary>
    /// 用于还原窗口
    /// </summary>

    public class WindowStatus
    {
        public object Root { get; set; }
        public Grid Parent { get; set; }

        public WindowStyle WindowStyle { get; set; }

        public WindowState WindowState { get; set; }

        public ResizeMode ResizeMode { get; set; }
    }

    /// <summary>
    /// ImageView.xaml 的交互逻辑
    /// </summary>
    public partial class ImageView : UserControl, IView
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(ImageView));
        public ToolBarTop ToolBarTop { get; set; }

        public View View { get; set; }
     
        public ImageView()
        {
            View = new View();
            InitializeComponent();
        }

        public ObservableCollection<IDrawingVisual> DrawingVisualLists { get; set; } = new ObservableCollection<IDrawingVisual>();

        private DrawingVisual DrawingVisualGrid = new DrawingVisual();

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            //这里是为了让控件可以被选中，作为做了一个底层的Textbox,这样就可以让控件被选中了，后面看看能不能优化掉，这个写法并不是好的。
            this.MouseDown += (s, e) =>
            {
                TextBox1.Focus();
            };

            ToolBarTop = new ToolBarTop(this,Zoombox1, ImageShow);
            ToolBar1.DataContext = ToolBarTop;
            ToolBarTop.ToolBarScaleRuler.ScalRuler.ScaleLocation = ScaleLocation.lowerright;
            ListView1.ItemsSource = DrawingVisualLists;


            this.Focusable = true;
            Zoombox1.LayoutUpdated += Zoombox1_LayoutUpdated;


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

            this.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.R)
                {
                    BorderPropertieslayers.Visibility = BorderPropertieslayers.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                }

            };
            this.PreviewKeyDown += (s,e)=>
            {
                if (e.Key == Key.Escape)
                {
                    if (DrawingVisualPolygonCache != null)
                    {
                        ImageShow.RemoveVisual(DrawingVisualPolygonCache);
                        DrawingVisualPolygonCache.Render();
                    }
                }
            };
        }



        private void Zoombox1_LayoutUpdated(object? sender, EventArgs e)
        {
            foreach (var item in DrawingVisualLists)
            {
                item.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                item.Render();
            }
        }



        private void DrawGridImage(DrawingVisual drawingVisual, BitmapSource bitmapImage)
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



        private static void DrawSelectRect(DrawingVisual drawingVisual, Rect rect)
        {
            using DrawingContext dc = drawingVisual.RenderOpen();
            dc.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#77F3F3F3")), new Pen(Brushes.Blue, 1), rect);
        }
        private DrawingVisual SelectRect = new DrawingVisual();

        private bool IsMouseDown;
        private Point MouseDownP;
        private DrawingVisualCircle? SelectDCircle;
        private DrawingVisualRectangle? SelectRectangle;
        private DrawingVisualCircle DrawCircleCache;
        private DrawingVisualRectangle DrawingRectangleCache;
        private DrawingVisualPolygon? DrawingVisualPolygonCache;


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




        private void ImageShow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DrawingVisualPolygonCache != null)
            {
                DrawingVisualPolygonCache.MovePoints = null;
                DrawingVisualPolygonCache.Render();
                DrawingVisualPolygonCache = null;
            }
        }


        private void ImageShow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas && !Keyboard.Modifiers.HasFlag(Zoombox1.ActivateOn))
            {
                drawCanvas.CaptureMouse();

                MouseDownP = e.GetPosition(drawCanvas);
                IsMouseDown = true;

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
                        DrawingVisualPolygonCache = new DrawingVisualPolygon();
                        DrawingVisualPolygonCache.Attribute.Pen.Thickness = 1 / Zoombox1.ContentMatrix.M11;
                        drawCanvas.AddVisual(DrawingVisualPolygonCache);
                    }
                }
                else
                {
                    if (drawCanvas.GetVisual(MouseDownP) is IDrawingVisual drawingVisual)
                    {
                        if (PropertyGrid2.SelectedObject is DrawBaseAttribute viewModelBase)
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
                                SelectRectangle = Rectangle;
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

        Point LastMouseMove;


        private void ImageShow_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas && (Zoombox1.ActivateOn == ModifierKeys.None || !Keyboard.Modifiers.HasFlag(Zoombox1.ActivateOn)))
            {
                var point = e.GetPosition(drawCanvas);
                if (ToolBarTop.DrawPolygon)
                {
                    if (DrawingVisualPolygonCache != null)
                    {
                        DrawingVisualPolygonCache.MovePoints = point;
                        DrawingVisualPolygonCache.Render();
                    }
                }

                if (IsMouseDown)
                {
                    if (ToolBarTop.EraseVisual)
                    {
                        DrawSelectRect(SelectRect, new Rect(MouseDownP, point)); ;
                    }
                    else if (ToolBarTop.DrawCircle)
                    {
                        if (DrawCircleCache != null)
                        {
                            double Radius = Math.Sqrt((Math.Pow(point.X - MouseDownP.X, 2) + Math.Pow(point.Y - MouseDownP.Y, 2)));
                            DrawCircleCache.Attribute.Radius = Radius;
                            DrawCircleCache.Render();
                        }
                    }
                    else if (ToolBarTop.DrawRect)
                    {
                        if (DrawingRectangleCache != null)
                        {
                            DrawingRectangleCache.Attribute.Rect = new Rect(MouseDownP, point);
                            DrawingRectangleCache.Render();
                        }
                    }
                    else if (SelectDCircle != null)
                    {
                        SelectDCircle.Attribute.Center += point - LastMouseMove;
                    }
                    else if (SelectRectangle != null)
                    {
                        var OldRect = SelectRectangle.Attribute.Rect;
                        SelectRectangle.Attribute.Rect = new Rect(OldRect.X + point.X - LastMouseMove.X, OldRect.Y + point.Y - LastMouseMove.Y, OldRect.Width, OldRect.Height);
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
                else if (ToolBarTop.DrawPolygon)
                {
                    if (DrawingVisualPolygonCache != null)
                    {
                        DrawingVisualPolygonCache.Points.Add(MouseUpP);
                        DrawingVisualPolygonCache.MovePoints = null;
                        DrawingVisualPolygonCache.Render();
                    }

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
        }

        private void ImageShow_MouseEnter(object sender, MouseEventArgs e)
        {
        }
        private void ImageShow_MouseLeave(object sender, MouseEventArgs e)
        {
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



        private WindowStatus OldWindowStatus { get; set; }

        private void Button8_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggleButton)
            {
                var window = Window.GetWindow(this);

                if (toggleButton.IsChecked == true)
                {
                    if (this.VisualParent is Grid p)
                    {
                        OldWindowStatus = new WindowStatus();
                        OldWindowStatus.Parent = p;
                        OldWindowStatus.WindowState = window.WindowState;
                        OldWindowStatus.WindowStyle = window.WindowStyle;
                        OldWindowStatus.ResizeMode = window.ResizeMode;
                        OldWindowStatus.Root = window.Content;
                        window.WindowStyle = WindowStyle.None;
                        window.WindowState = WindowState.Maximized;

                        OldWindowStatus.Parent.Children.Remove(this);
                        window.Content = this;
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {

                    window.WindowStyle = OldWindowStatus.WindowStyle;
                    window.WindowState = OldWindowStatus.WindowState;
                    window.ResizeMode = OldWindowStatus.ResizeMode;

                    window.Content = OldWindowStatus.Root;
                    OldWindowStatus.Parent.Children.Add(this);
                }
            }
        }




        [UnmanagedCallersOnly(CallConvs = new Type[] { typeof(CallConvCdecl) })]
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
                OpenCVHelper.RtlMoveMemory(writeableBitmap.BackBuffer, buff, (uint)(cols * rows * type));
                writeableBitmap.Lock();
                writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
                writeableBitmap.Unlock();
                if (ViewGridManager.GetInstance().Views[3] is ImageView view)
                {
                    view.ImageShow.Source = writeableBitmap;
                }
            });
            return 0;
        }

        public unsafe void OpenCVImage(string? filePath)
        {
            OpenCVHelper.SetInitialFrame((nint)(delegate* unmanaged[Cdecl]<IntPtr, int, int, int, int>)(&InitialFrame));

            if (filePath != null && File.Exists(filePath))
            {
                ToolBar1.Visibility = Visibility.Visible;
            }
        }


        public void OpenImage(CVCIEFileInfo fileInfo)
        {
            if (fileInfo.fileType == MQTTMessageLib.FileServer.FileExtType.Src) OpenImage(fileInfo.data);
            else if(fileInfo.fileType == MQTTMessageLib.FileServer.FileExtType.Raw)
            {
                ShowImage(fileInfo);
            }
        }

        private void ShowImage(CVCIEFileInfo fileInfo)
        {
            logger.Info("OpenImage .....");

            OpenCvSharp.Mat src = new OpenCvSharp.Mat(fileInfo.height, fileInfo.width, OpenCvSharp.MatType.MakeType(fileInfo.depth, fileInfo.channels), fileInfo.data);
            SetImageSource(src.ToBitmapSource());
        }

        public void OpenImage(byte[] data)
        {
            if (data != null)
            {
                logger.Info("OpenImage .....");
                var src = OpenCvSharp.Cv2.ImDecode(data, OpenCvSharp.ImreadModes.Unchanged);
                SetImageSource(src.ToBitmapSource());
            }
        }

        public void Clear()
        {
            ImageShow.Source = null;

        }

        public void OpenImage(string? filePath)
        {
            if (filePath != null && File.Exists(filePath))
            {
                string ext = Path.GetExtension(filePath).ToLower(CultureInfo.CurrentCulture);
                if (ext == ".tif")
                {
                    BitmapImage bitmapImage = new BitmapImage(new Uri(filePath));
                    SetImageSource(bitmapImage);
                }
                else if (ext == ".cvraw")
                {
                    CVCIEFileInfo fileInfo = new CVCIEFileInfo();
                    fileInfo.fileType = MQTTMessageLib.FileServer.FileExtType.Raw;
                    int ret = CVFileUtils.ReadCVFile_Raw(filePath, ref fileInfo);
                    if (ret == 0)
                    {
                        OpenImage(fileInfo);
                    }
                }
                else
                {
                    BitmapImage bitmapImage = new BitmapImage(new Uri(filePath));
                    SetImageSource(bitmapImage);
                }
            }
        }

        public void OpenGhostImage(string? filePath,int[] LEDpixelX, int[] LEDPixelY, int[] GhostPixelX, int[] GhostPixelY)
        {
            if (filePath == null)
                return;

            int i = OpenCVHelper.ReadGhostImage(filePath, LEDpixelX.Length, LEDpixelX, LEDPixelY, GhostPixelX.Length, GhostPixelX, GhostPixelY, out HImage hImage);
            if (i != 0) return;
            var writeableBitmap =HImageToWriteableBitmap(hImage);
            ViewBitmapSource = writeableBitmap;
            ImageShow.Source = ViewBitmapSource;
            i = OpenCVHelper.ReadGhostHImage(hImage, out HImage hImage1);
            if (i != 0) return;
            PseudoImage = HImageToWriteableBitmap(hImage1);

            Task.Run(() => {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Zoombox1.ZoomUniform();
                });
            });
        }




        private static WriteableBitmap HImageToWriteableBitmap(HImage hImage)
        {
            PixelFormat format = hImage.channels switch
            {
                1 => PixelFormats.Gray8,
                3 => PixelFormats.Bgr24,
                4 => PixelFormats.Bgr32,
                _ => PixelFormats.Default,
            };

            WriteableBitmap writeableBitmap = new WriteableBitmap(hImage.cols, hImage.rows, 96.0, 96.0, format, null);
            OpenCVHelper.RtlMoveMemory(writeableBitmap.BackBuffer, hImage.pData, (uint)(hImage.cols * hImage.rows * hImage.channels));
            writeableBitmap.Lock();
            writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
            writeableBitmap.Unlock();
            return writeableBitmap;
        }



        private void SetImageSource(BitmapSource bitmapImage)
        {
            ViewBitmapSource = bitmapImage;
            ImageShow.Source = ViewBitmapSource;
            DrawGridImage(DrawingVisualGrid, bitmapImage);
            Zoombox1.ZoomUniform();
            ToolBar1.Visibility = Visibility.Visible;
            ImageShow.ImageInitialize();
        }

        private void SetImageSource(BitmapImage bitmapImage)
        {
            ViewBitmapSource = bitmapImage;
            ImageShow.Source = ViewBitmapSource;
            DrawGridImage(DrawingVisualGrid, bitmapImage);
            Task.Run(() => {
                Application.Current.Dispatcher.Invoke(()=>
                {
                    Zoombox1.ZoomUniform();
                });
            });
            ToolBar1.Visibility = Visibility.Visible;
            ImageShow.ImageInitialize();
        }
        public ImageSource PseudoImage { get; set; }

        public ImageSource ViewBitmapSource { get; set; }


        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggleButton)
            {
                if (PseudoImage != null)
                {
                    if (toggleButton.IsChecked == true)
                    {
                        ImageShow.Source = PseudoImage;
                    }
                    else
                    {
                        ImageShow.Source = ViewBitmapSource;
                    }
                }
                else
                {
                    PseudoColor window = new PseudoColor() { Owner = Window.GetWindow(this)};
                    window.Show();
                }
            }
        }

        private void ToolBar1_Loaded(object sender, RoutedEventArgs e)
        {

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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                OpenImage(openFileDialog.FileName);
            }
            
        }

        public void AddPoint(List<Point> points)
        {
            int id =0;
            foreach (var item in points)
            {
                id++;
                DrawingVisualCircleWord Circle = new DrawingVisualCircleWord();
                Circle.Attribute.Center = item;
                Circle.Attribute.Radius = 20 / Zoombox1.ContentMatrix.M11;
                Circle.Attribute.Brush = Brushes.Transparent;
                Circle.Attribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                Circle.Attribute.ID = id;
                Circle.Render();
                ImageShow.AddVisual(Circle);
            }
        }

        public void AddRect(Rect rect)
        {
            DrawingVisualRectangleWord Rectangle = new DrawingVisualRectangleWord();
            Rectangle.Attribute.Rect = new Rect(rect.X, rect.Y, rect.Width, rect.Height);
            Rectangle.Attribute.Brush = Brushes.Transparent;
            Rectangle.Attribute.Pen = new Pen(Brushes.Red, rect.Width / 30.0);
            Rectangle.Render();
            ImageShow.AddVisual(Rectangle);
        }

        public void AddPOIPoint(List<POIPoint> PoiPoints)
        {
            foreach (var item in PoiPoints)
            {
                switch (item.PointType)
                {   
                    case POIPointTypes.Circle:
                        DrawingVisualCircleWord Circle = new DrawingVisualCircleWord();
                        Circle.Attribute.Center = new Point(item.PixelX, item.PixelY);
                        Circle.Attribute.Radius = item.Radius;
                        Circle.Attribute.Brush = Brushes.Transparent;
                        Circle.Attribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                        Circle.Attribute.ID = item.Id;
                        Circle.Attribute.Text = item.Name;
                        Circle.Render();
                        ImageShow.AddVisual(Circle);
                        break;
                    case POIPointTypes.Rect:
                        DrawingVisualRectangleWord Rectangle = new DrawingVisualRectangleWord();
                        Rectangle.Attribute.Rect = new Rect(item.PixelX, item.PixelY, item.Width, item.Height);
                        Rectangle.Attribute.Brush = Brushes.Transparent;
                        Rectangle.Attribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                        Rectangle.Attribute.ID = item.Id;
                        Rectangle.Attribute.Name = item.Name;
                        Rectangle.Render();
                        ImageShow.AddVisual(Rectangle);
                        break;
                    case POIPointTypes.Mask:
                        break;
                    default:
                        break;
                }

            }
        }

        public void ResetPOIPoint()
        {
            ImageShow.Clear();
        }

        private void reference_Click(object sender, RoutedEventArgs e)
        {
            menuPop1.IsOpen = true;
        }

        private void reference1_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tag)
            {
                int i = int.Parse(tag);
                if (i == -1)
                {
                    ToolBarTop.ToolConcentricCircle.IsShow = false;
                }
                else 
                {
                    ToolBarTop.ToolConcentricCircle.IsShow = true;
                    ToolBarTop.ToolConcentricCircle.Mode = i;
                }
                menuPop1.IsOpen = false;
            }
        }

        private void RangeSlider1_ValueChanged(object sender, RoutedPropertyChangedEventArgs<HandyControl.Data.DoubleRange> e)
        {
            RowDefinitionStart.Height = new GridLength((170.0 / 255.0) * (255 - RangeSlider1.ValueEnd));
            RowDefinitionEnd.Height = new GridLength((170.0 / 255.0) * RangeSlider1.ValueStart);
        }
    }
}
