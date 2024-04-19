#pragma warning disable CS8625
using ColorVision.Common.Utilities;
using ColorVision.Draw;
using ColorVision.Draw.Ruler;
using ColorVision.Common.MVVM;
using ColorVision.Net;
using ColorVision.Util.Draw.Special;
using log4net;
using MQTTMessageLib.Algorithm;
using MQTTMessageLib.FileServer;
using OpenCvSharp.WpfExtensions;
using SkiaSharp.Views.WPF;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ColorVision.Solution;
using System.Linq;
using ColorVision.Utils;
using CVCommCore.CVAlgorithm;

namespace ColorVision.Media
{
    /// <summary>
    /// ImageView.xaml 的交互逻辑
    /// </summary>
    public partial class ImageView : UserControl, IView,IDisposable
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(ImageView));
        public ToolBarTop ToolBarTop { get; set; }

        public ColormapTypes ColormapTypes { get; set; } = ColormapTypes.COLORMAP_JET;

        public View View { get; set; }
     
        public ImageView()
        {
            View = new View();
            InitializeComponent();
        }
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public ObservableCollection<IDrawingVisual> DrawingVisualLists { get; set; } = new ObservableCollection<IDrawingVisual>();

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            //这里是为了让控件可以被选中，作为做了一个底层的Textbox,这样就可以让控件被选中了，后面看看能不能优化掉，这个写法并不是好的。
            MouseDown += (s, e) =>
            {
                TextBox1.Focus();
            };
            ToolBarTop = new ToolBarTop(this,Zoombox1, ImageShow);
            ToolBar1.DataContext = ToolBarTop;
            ToolBar2.DataContext = ToolBarTop;
            ToolBarTop.ToolBarScaleRuler.ScalRuler.ScaleLocation = ScaleLocation.lowerright;
            ListView1.ItemsSource = DrawingVisualLists;

            ToolBarTop.ClearImageEventHandler += (s, e) => Clear();

            Focusable = true;
            Zoombox1.LayoutUpdated += Zoombox1_LayoutUpdated;


            ImageShow.VisualsAdd += (s, e) =>
            {
                if (s is IDrawingVisual visual && !DrawingVisualLists.Contains(visual) && s is Visual visual1)
                {
                    DrawingVisualLists.Add(visual);
                    visual.BaseAttribute.PropertyChanged += (s1, e1) =>
                    {
                        if (e1.PropertyName == "IsShow")
                        {
                            ListView1.ScrollIntoView(visual);
                            ListView1.SelectedIndex = DrawingVisualLists.IndexOf(visual);
                            if (visual.BaseAttribute.IsShow == true)
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
                    if (visual.BaseAttribute.IsShow)
                        DrawingVisualLists.Remove(visual);
                }
            };

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.R)
                {
                    BorderPropertieslayers.Visibility = BorderPropertieslayers.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                }

            };
            PreviewKeyDown += (s,e)=>
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


            AllowDrop = true;
            Drop += ImageView_Drop; ;
        }

        private void ImageView_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var sarr = e.Data.GetData(DataFormats.FileDrop);
                var a = sarr as string[];
                var fn = a?.First();

                if (File.Exists(fn))
                {
                    if (fn.EndsWith("cvraw", StringComparison.OrdinalIgnoreCase))
                    {
                        CVFileUtil.ReadCVRaw(fn, out CVCIEFile fileInfo);
                        OpenImage(fileInfo);
                    }
                    else if (Tool.IsImageFile(fn))
                    {
                        OpenImage(fn);
                    }
                    else if (File.Exists(fn))
                    {
                        PlatformHelper.Open(fn);
                    }

                }
                else if (Directory.Exists(fn))
                {

                }
            }
        }

        private void Zoombox1_LayoutUpdated(object? sender, EventArgs e)
        {
            foreach (var item in DrawingVisualLists)
            {
                item.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                item.Render();
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
                    drawing.BaseAttribute.IsShow = false;
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
                        PropertyGrid2.SelectedObject = drawingVisual.BaseAttribute;
                        drawingVisual.BaseAttribute.PropertyChanged += (s, e) =>
                        {
                            PropertyGrid2.Refresh();
                        };

                        ListView1.ScrollIntoView(drawingVisual);
                        ListView1.SelectedIndex = DrawingVisualLists.IndexOf(drawingVisual);

                        if (ToolBarTop.ImageEditMode == true)
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
        public void OpenImage(CVCIEFile fileInfo)
        {
            if (fileInfo.FileExtType == FileExtType.Tif)
            {
                var src = OpenCvSharp.Cv2.ImDecode(fileInfo.data, OpenCvSharp.ImreadModes.Unchanged);
                SetImageSource(src.ToWriteableBitmap());
            }
            else if (fileInfo.FileExtType == FileExtType.Raw || fileInfo.FileExtType == FileExtType.Src)
            {
                logger.Info("OpenImage .....");

                OpenCvSharp.Mat src = new OpenCvSharp.Mat(fileInfo.cols, fileInfo.rows, OpenCvSharp.MatType.MakeType(fileInfo.Depth, fileInfo.channels), fileInfo.data);
                OpenCvSharp.Mat dst = null;
                if (fileInfo.bpp == 32)
                {
                    OpenCvSharp.Cv2.Normalize(src, src, 0, 255, OpenCvSharp.NormTypes.MinMax);
                    dst = new OpenCvSharp.Mat();
                    src.ConvertTo(dst, OpenCvSharp.MatType.CV_8U);
                }
                else
                {
                    dst = src;
                }
                SetImageSource(dst.ToWriteableBitmap());
                dst.Dispose();
                src.Dispose();
            }
            else if (fileInfo.FileExtType == FileExtType.CIE)
            {
                logger.Info("OpenImage .....");

                OpenCvSharp.Mat src = new OpenCvSharp.Mat(fileInfo.cols, fileInfo.rows, OpenCvSharp.MatType.MakeType(fileInfo.Depth, fileInfo.channels), fileInfo.data);
                OpenCvSharp.Mat dst = null;
                if (fileInfo.bpp == 32)
                {
                    OpenCvSharp.Cv2.Normalize(src, src, 0, 255, OpenCvSharp.NormTypes.MinMax);
                    dst = new OpenCvSharp.Mat();
                    src.ConvertTo(dst, OpenCvSharp.MatType.CV_8U);
                }
                else
                {
                    dst = src;
                }
                var Data = dst.ToWriteableBitmap();
                SetImageSource(Data);
                dst.Dispose();
                src.Dispose();
            }
        }

        public void Clear()
        {
            PseudoImage = null;
            ViewBitmapSource = null;

            ImageShow.Source = null;

            if (HImageCache != null)
            {
                HImageCache?.Dispose();
                HImageCache = null;
            }
            GC.Collect();
        }

        public void OpenImage(string? filePath)
        {
            if (filePath != null && File.Exists(filePath))
            {
                string ext = Path.GetExtension(filePath).ToLower(CultureInfo.CurrentCulture);
                if (ext.Contains(".cvraw") || ext.Contains(".cvsrc") || ext.Contains(".cvcie"))
                {
                    FileExtType fileExtType = ext.Contains(".cvraw") ? FileExtType.Raw : ext.Contains(".cvsrc") ? FileExtType.Src : FileExtType.CIE;
                    try
                    {
                        OpenImage(new NetFileUtil().OpenLocalCVFile(filePath, fileExtType));
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                }
                else
                {
                    try
                    {
                        BitmapImage bitmapImage = new BitmapImage(new Uri(filePath));
                        SetImageSource(bitmapImage);
                    }
                    catch(Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }

                }
            }
        }


        public void OpenGhostImage(string? filePath,int[] LEDpixelX, int[] LEDPixelY, int[] GhostPixelX, int[] GhostPixelY)
        {
            if (filePath == null)
                return;

            int i = OpenCVHelper.ReadGhostImage(filePath, LEDpixelX.Length, LEDpixelX, LEDPixelY, GhostPixelX.Length, GhostPixelX, GhostPixelY, out HImage hImage);
            if (i != 0) return;
            SetImageSource(hImage.ToWriteableBitmap());
            OpenCVHelper.FreeHImageData(hImage.pData);
            hImage.pData = IntPtr.Zero;
        }

        public HImage? HImageCache { get; set; }


        private void SetImageSource(BitmapImage writeableBitmap)
        {
            if (HImageCache != null)
            {
                HImageCache?.Dispose();
                HImageCache = null;
            };
            Task.Run(() => Application.Current.Dispatcher.Invoke((() => HImageCache = writeableBitmap.ToHImage())));
           
            ToolBarTop.PseudoVisible = Visibility.Visible;

            PseudoSlider.ValueChanged -= RangeSlider1_ValueChanged;
            PseudoSlider.ValueChanged += RangeSlider1_ValueChanged;

            if (HImageCache?.channels == 1)
            {
                ToolBarTop.CIEVisible = Visibility.Collapsed;
            }
            else
            {
                ToolBarTop.CIEVisible = Visibility.Visible;
            }
            ViewBitmapSource = writeableBitmap;
            ImageShow.Source = ViewBitmapSource;

            Task.Run(() => {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Zoombox1.ZoomUniform();
                    ToolBarTop.ToolBarScaleRuler.Render();
                });
            });
            ToolBar1.Visibility = Visibility.Visible;
            ImageShow.ImageInitialize();
            ToolBarTop.ToolBarScaleRuler.IsShow = true;
        }

        private void SetImageSource(WriteableBitmap writeableBitmap)
        {
            if (HImageCache != null)
            {
                HImageCache?.Dispose();
                HImageCache = null;
            };

            Task.Run(() => Application.Current.Dispatcher.Invoke((() => HImageCache = writeableBitmap.ToHImage())));

            ToolBarTop.PseudoVisible = Visibility.Visible;

            PseudoSlider.ValueChanged -= RangeSlider1_ValueChanged;
            PseudoSlider.ValueChanged += RangeSlider1_ValueChanged;

            if (HImageCache?.channels == 1)
            {
                ToolBarTop.CIEVisible = Visibility.Collapsed;
            }
            else
            {
                ToolBarTop.CIEVisible = Visibility.Visible;
            }
            ViewBitmapSource = writeableBitmap;
            ImageShow.Source = ViewBitmapSource;

            Task.Run(() => {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Zoombox1.ZoomUniform();
                    ToolBarTop.ToolBarScaleRuler.Render();
                });
            });
            ToolBar1.Visibility = Visibility.Visible;
            ImageShow.ImageInitialize();
            ToolBarTop.ToolBarScaleRuler.IsShow = true;
        }

        public ImageSource PseudoImage { get; set; }
        public ImageSource ViewBitmapSource { get; set; }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            RenderPseudo();
        }
        private void Pseudo_MouseDoubleClick(object sender, RoutedEventArgs e)
        {
            PseudoColor pseudoColor = new PseudoColor();
            pseudoColor.Closed += (s, e) =>
            {
                ColormapTypes = pseudoColor.ColormapTypes;
            };
            pseudoColor.Show();
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

                PropertyGrid2.SelectedObject = drawingVisual.BaseAttribute;
                drawingVisual.BaseAttribute.PropertyChanged += (s, e) =>
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
                        Circle.Attribute.ID = item.Id ?? -1;
                        Circle.Attribute.Text = item.Name;
                        Circle.Render();
                        ImageShow.AddVisual(Circle);
                        break;
                    case POIPointTypes.Rect:
                        DrawingVisualRectangleWord Rectangle = new DrawingVisualRectangleWord();
                        Rectangle.Attribute.Rect = new Rect(item.PixelX, item.PixelY, item.Width, item.Height);
                        Rectangle.Attribute.Brush = Brushes.Transparent;
                        Rectangle.Attribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                        Rectangle.Attribute.ID = item.Id ?? -1;
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

        public void RenderPseudo()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Pseudo.IsChecked == false)
                {
                    ImageShow.Source = ViewBitmapSource;
                    PseudoImage = null;
                    return;
                }

                if (HImageCache != null)
                {
                    // 首先获取滑动条的值，这需要在UI线程中执行

                    uint min = (uint)PseudoSlider.ValueStart;
                    uint max = (uint)PseudoSlider.ValueEnd;

                    logger.Info($"ImagePath，正在执行PseudoColor,min:{min},max:{max}");
                    Task.Run(() =>
                    {
                        int ret = OpenCVHelper.PseudoColor((HImage)HImageCache, out HImage hImageProcessed, min, max, ColormapTypes);

                        var image = hImageProcessed.ToWriteableBitmap();
                        OpenCVHelper.FreeHImageData(hImageProcessed.pData);
                        hImageProcessed.pData = IntPtr.Zero;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (ret == 0)
                            {
                                PseudoImage = image;
                                if (Pseudo.IsChecked == true)
                                {
                                    ImageShow.Source = PseudoImage;
                                }

                            }
                        });
                    });
                };
            });
        }

        private void RangeSlider1_ValueChanged(object sender, RoutedPropertyChangedEventArgs<HandyControl.Data.DoubleRange> e)
        {
            RowDefinitionStart.Height = new GridLength((170.0 / 255.0) * (255 - PseudoSlider.ValueEnd));
            RowDefinitionEnd.Height = new GridLength((170.0 / 255.0) * PseudoSlider.ValueStart);
            DebounceTimer.AddOrResetTimer("RenderPseudo",100, RenderPseudo);
        }

        private void ButtonCIE1931_Click(object sender, RoutedEventArgs e)
        {
            bool old = ToolBarTop.ShowImageInfo;
            ToolBarTop.ShowImageInfo = true; 
            WindowCIE windowCIE = new WindowCIE();
            windowCIE.Owner = Window.GetWindow(this);

            MouseMoveColorHandler mouseMoveColorHandler = (s, e) =>
            {
                windowCIE.ChangeSelect(e);
            };

            ToolBarTop.MouseMagnifier.MouseMoveColorHandler += mouseMoveColorHandler;
            windowCIE.Closed += (s, e) =>
            {
                ToolBarTop.MouseMagnifier.MouseMoveColorHandler -= mouseMoveColorHandler;
                ToolBarTop.ShowImageInfo = old;
            };
            windowCIE.Show();
        }


    }
}
