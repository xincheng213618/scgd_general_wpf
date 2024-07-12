#pragma warning disable CS8625
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Draw;
using ColorVision.Draw.Ruler;
using ColorVision.Net;
using ColorVision.UI.Views;
using ColorVision.Util.Draw.Special;
using cvColorVision;
using log4net;
using MQTTMessageLib.FileServer;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Media
{
    public class ImageViewConfig:ViewModelBase
    {
        [JsonIgnore]
        public IntPtr ConvertXYZhandle { get; set; } = Tool.GenerateRandomIntPtr();

        [JsonIgnore]
        public bool ConvertXYZhandleOnce { get; set; }

        public ColormapTypes ColormapTypes { get => _ColormapTypes; set { _ColormapTypes = value; NotifyPropertyChanged(); } }
        private ColormapTypes _ColormapTypes = ColormapTypes.COLORMAP_JET;

       [JsonIgnore]
        public string FilePath { get; set; }
        [JsonIgnore]
        public bool IsCVCIE => FilePath != null && FilePath.Contains("cvcie") && CVFileUtil.IsCIEFile(FilePath);

        [JsonIgnore]
        public string CVCIEFilePath { get; set; }

        [JsonIgnore]
        public int Channel { get => _Channel; set { _Channel = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(IsChannel1)); NotifyPropertyChanged(nameof(IsChannel3)); } }
        private int _Channel;

        [JsonIgnore]
        public bool IsChannel1 => Channel == 1;
        [JsonIgnore]
        public bool IsChannel3 => Channel == 3;

        public bool IsShowLoadImage { get => _IsShowLoadImage; set { _IsShowLoadImage = value; NotifyPropertyChanged(); } }
        private bool _IsShowLoadImage = true;
    }


    /// <summary>
    /// ImageView.xaml 的交互逻辑
    /// </summary>
    public partial class ImageView : UserControl, IView,IDisposable, INotifyPropertyChanged
    {
        public static List<ImageView> Views { get; set; } = new List<ImageView>();
        public static ImageView GetInstance()
        {
            foreach (var item in Views)
            {
                if (item.Parent == null)
                    return item;
            }
            ImageView imageView = new ImageView();
            Views.Add(imageView);
            return imageView;
        }

        private static readonly ILog logger = LogManager.GetLogger(typeof(ImageView));
        public ToolBarTop ToolBarTop { get; set; }

        public View View { get; set; }

        public ImageViewConfig Config { get; set; }
        public ImageView()
        {
            Config = new ImageViewConfig();
            View = new View();
            InitializeComponent();
        }


        public void SetConfig(ImageViewConfig imageViewConfig)
        {
            Config = imageViewConfig;
            this.DataContext = Config;
        }


        public event PropertyChangedEventHandler? PropertyChanged;
        /// <summary>
        /// 消息通知事件
        /// </summary>
        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public ObservableCollection<IDrawingVisual> DrawingVisualLists { get; set; } = new ObservableCollection<IDrawingVisual>();

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ToolBarTop = new ToolBarTop(this,Zoombox1, ImageShow);
            ToolBar1.DataContext = ToolBarTop;
            ToolBar2.DataContext = ToolBarTop;
            ToolBarTop.ToolBarScaleRuler.ScalRuler.ScaleLocation = ScaleLocation.lowerright;
            ListView1.ItemsSource = DrawingVisualLists;

            ToolBarTop.ClearImageEventHandler += Clear;
            Focusable = true;
            Zoombox1.LayoutUpdated += Zoombox1_LayoutUpdated;
            ImageShow.VisualsAdd += ImageShow_VisualsAdd;
            ImageShow.VisualsRemove += ImageShow_VisualsRemove;
            PreviewKeyDown += ImageView_PreviewKeyDown;
            this.MouseDown += (s, e) => this.Focus();

            AllowDrop = true;
            Drop += ImageView_Drop;

        }

        public void Clear(object? sender, EventArgs e)
        {
            Clear();
        }


        private void ImageShow_VisualsAdd(object? sender, EventArgs e)
        {
            if (sender is IDrawingVisual visual && !DrawingVisualLists.Contains(visual) && sender is Visual visual1)
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

        }

        private void ImageShow_VisualsRemove(object? sender, EventArgs e)
        {
            if (sender is IDrawingVisual visual)
                if (visual.BaseAttribute.IsShow)
                    DrawingVisualLists.Remove(visual);
        }

        private void ImageView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (DrawingVisualPolygonCache != null)
                {
                    ImageShow.RemoveVisual(DrawingVisualPolygonCache);
                    DrawingVisualPolygonCache.Render();
                }
            }
            else if (e.Key == Key.R)
            {
                BorderPropertieslayers.Visibility = BorderPropertieslayers.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            }
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
                        OpenImage(fileInfo.ToWriteableBitmap());
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
        private DrawingVisual SelectRect = new();

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
                MenuItem menuItem = new() { Header = "隐藏(_H)" };
                menuItem.Click += (s, e) =>
                {
                    drawing.BaseAttribute.IsShow = false;
                };
                MenuItem menuIte2 = new() { Header = ColorVision.Engine.Properties.Resources.MenuDelete };
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

            int result = ConvertXYZ.CM_ReleaseBuffer(Config.ConvertXYZhandle);
        }

        public void OpenImage(WriteableBitmap? writeableBitmap)
        {
           if (writeableBitmap != null) 
                SetImageSource(writeableBitmap);
        }

        public void OpenImage(string? filePath)
        {
            if (filePath != null && File.Exists(filePath))
            {
                long fileSize = new FileInfo(filePath).Length;
                bool isLargeFile = fileSize > 1024 * 1024 * 100;//例如，文件大于1MB时认为是大文件

                string ext = Path.GetExtension(filePath).ToLower(CultureInfo.CurrentCulture);
                if (ext.Contains(".cvraw") || ext.Contains(".cvsrc") || ext.Contains(".cvcie"))
                {
                    FileExtType fileExtType = ext.Contains(".cvraw") ? FileExtType.Raw : ext.Contains(".cvsrc") ? FileExtType.Src : FileExtType.CIE;
                    IsCVCIE = fileExtType == FileExtType.CIE;
                    try
                    {
                        if (Config.IsShowLoadImage && isLargeFile)
                        {
                            WaitControl.Visibility = Visibility.Visible;
                            Task.Run(() =>
                            {
                                CVCIEFile cVCIEFile = new NetFileUtil().OpenLocalCVFile(filePath, fileExtType);
                                Config.FilePath = filePath;
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    OpenImage(cVCIEFile.ToWriteableBitmap());

                                    WaitControl.Visibility = Visibility.Collapsed;
                                });
                            });
                        }
                        else
                        {
                            CVCIEFile cVCIEFile = new NetFileUtil().OpenLocalCVFile(filePath, fileExtType);
                            Config.FilePath = filePath;
                            OpenImage(cVCIEFile.ToWriteableBitmap());
                        };
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

                        if (Config.IsShowLoadImage && isLargeFile)
                        {
                            WaitControl.Visibility = Visibility.Visible;
                            Config.FilePath = filePath;
                            Task.Run(() =>
                            {
                                byte[] imageData = File.ReadAllBytes(filePath);
                                BitmapImage bitmapImage = ImageUtils.CreateBitmapImage(imageData);

                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    SetImageSource(bitmapImage);
                                    WaitControl.Visibility = Visibility.Collapsed;
                                });
                            });

                        }
                        else
                        {
                            Config.FilePath = filePath;
                            BitmapImage bitmapImage = new BitmapImage(new Uri(filePath));
                            SetImageSource(bitmapImage);
                        };

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
            ToolBarTop.CIEVisible = Visibility.Collapsed;
            PseudoStackPanel.Visibility = Visibility.Collapsed ;
            if (HImageCache != null)
            {
                HImageCache?.Dispose();
                HImageCache = null;
            };

            Task.Run(() => Application.Current.Dispatcher.Invoke((() =>
            {
                HImageCache = writeableBitmap.ToHImage();
                if (HImageCache is HImage hImage)
                {
                    Config.Channel = hImage.channels;
                    if (Config.IsCVCIE && Config.Channel ==1)
                        ToolBarTop.CIEVisible = Visibility.Visible;
                    if (Config.Channel == 1)
                        PseudoStackPanel.Visibility = Visibility.Visible; 
                }
            }
            )));

            ToolBarTop.PseudoVisible = Visibility.Visible;

            PseudoSlider.ValueChanged -= RangeSlider1_ValueChanged;
            PseudoSlider.ValueChanged += RangeSlider1_ValueChanged;


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

            ToolBarTop.MouseMagnifier.MouseMoveColorHandler -= ShowCVCIE;

            if (Config.IsCVCIE)
            {
                if (!Config.ConvertXYZhandleOnce)
                {
                    int result = ConvertXYZ.CM_InitXYZ(Config.ConvertXYZhandle);
                    logger.Info($"ConvertXYZ.CM_InitXYZ :{result}");
                    Config.ConvertXYZhandleOnce = true;
                }


                int index = CVFileUtil.ReadCIEFileHeader(Config.FilePath, out CVCIEFile cVCIEFile);
                CVFileUtil.ReadCIEFileData(Config.FilePath, ref cVCIEFile, index);

                int resultCM_SetBufferXYZ = ConvertXYZ.CM_SetBufferXYZ(ConvertXYZ.Handle, (uint)cVCIEFile.rows, (uint)cVCIEFile.cols, (uint)cVCIEFile.bpp, (uint)cVCIEFile.channels, cVCIEFile.data);
                logger.Debug($"CM_SetBufferXYZ :{resultCM_SetBufferXYZ}");

                ToolBarTop.MouseMagnifier.MouseMoveColorHandler += ShowCVCIE;
            }
        }
        public ImageSource PseudoImage { get; set; }
        public ImageSource ViewBitmapSource { get; set; }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            RenderPseudo();
        }
        private void Pseudo_MouseDoubleClick(object sender, RoutedEventArgs e)
        {
            PseudoColor pseudoColor = new PseudoColor(Config);
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


        public void AddVisual(Visual visual) => ImageShow.AddVisual(visual);


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
                        int ret = OpenCVHelper.PseudoColor((HImage)HImageCache, out HImage hImageProcessed, min, max, Config.ColormapTypes);

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
        public bool IsCVCIE { get => _IsCVCIE; set { _IsCVCIE = value; NotifyPropertyChanged(); } }
        private bool _IsCVCIE;
        private bool disposedValue;

        public void ShowCVCIE(object sender, ImageInfo imageInfo)
        {
            float dXVal = 0;
            float dYVal = 0;
            float dZVal = 0;
            float dx = 0, dy = 0, du = 0, dv = 0;
            _= ConvertXYZ.CM_GetXYZxyuvRect(ConvertXYZ.Handle, imageInfo.X, imageInfo.Y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, DefalutTextAttribute.Defalut.CVCIENum, DefalutTextAttribute.Defalut.CVCIENum);
            ToolBarTop.MouseMagnifier.DrawImageCVCIE(imageInfo, dXVal, dYVal, dZVal, dx, dy, du, dv);
        }


        private void ButtonCIE1931_Click(object sender, RoutedEventArgs e)
        {
            bool old = ToolBarTop.ShowImageInfo;
            ToolBarTop.ShowImageInfo = true; 
            WindowCIE windowCIE = new();
            windowCIE.Owner = Window.GetWindow(this);
            MouseMoveColorHandler mouseMoveColorHandler = (s, e) =>
            {
                if (IsCVCIE) 
                {
                    try
                    {
                        int xx = e.X;
                        int yy = e.Y;
                        float dXVal = 0;
                        float dYVal = 0;
                        float dZVal = 0;
                        float dx =0, dy =0,du = 0,dv = 0;

                        int result = ConvertXYZ.CM_GetXYZxyuvRect(ConvertXYZ.Handle, xx, yy, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, DefalutTextAttribute.Defalut.CVCIENum, DefalutTextAttribute.Defalut.CVCIENum);
                        logger.Debug($"CM_GetXYZxyuvRect :{result},res");

                        windowCIE.ChangeSelect(dx, dy);
                    }
                    catch 
                    {

                    }
                }
                else
                {
                    windowCIE.ChangeSelect(e);
                }
            };
            ToolBarTop.MouseMagnifier.MouseMoveColorHandler += mouseMoveColorHandler;
            windowCIE.Closed += (s, e) =>
            {
                ToolBarTop.MouseMagnifier.MouseMoveColorHandler -= mouseMoveColorHandler;
                ToolBarTop.ShowImageInfo = old;
            };
            windowCIE.Show();
        }
        private void HistogramButton_Click(object sender, RoutedEventArgs e)
        {
            RenderHistogram();
        }
        private static void DrawHistogram(int[] histogram, Color color, DrawingContext drawingContext, double width, double height)
        {
            double max = histogram.Max();
            double scale = height / max;

            Pen pen = new Pen(new SolidColorBrush(color), 1);

            for (int i = 0; i < histogram.Length; i++)
            {
                double x = i * (width / 256);
                double y = height - (histogram[i] * scale);
                drawingContext.DrawLine(pen, new Point(x, height), new Point(x, y));
            }
        }


        private void RenderHistogram()
        {
            if (ImageShow.Source is not BitmapSource bitmapSource)
                return;

            int width = bitmapSource.PixelWidth;
            int height = bitmapSource.PixelHeight;
            int stride = width * ((bitmapSource.Format.BitsPerPixel + 7) / 8);
            byte[] pixelData = new byte[height * stride];
            bitmapSource.CopyPixels(pixelData, stride, 0);

            int[] redHistogram = new int[256];
            int[] greenHistogram = new int[256];
            int[] blueHistogram = new int[256];

            for (int i = 0; i < pixelData.Length; i += 4) // Assuming a 32bpp image
            {
                byte blue = pixelData[i];
                byte green = pixelData[i + 1];
                byte red = pixelData[i + 2];

                redHistogram[red]++;
                greenHistogram[green]++;
                blueHistogram[blue]++;
            }

            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                double width1 = 256; // Width of the histogram
                double height1 = 100; // Height of the histogram

                // Draw each color channel histogram
                DrawHistogram(redHistogram, Colors.Red, drawingContext, width1, height1);
                DrawHistogram(greenHistogram, Colors.Green, drawingContext, width1, height1);
                DrawHistogram(blueHistogram, Colors.Blue, drawingContext, width1, height1);
            }

            RenderTargetBitmap bmp = new RenderTargetBitmap(256, 100, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(drawingVisual);

            Image image = new Image() { Margin = new Thickness(5) };

            image.Source = bmp; // histogramImage is an Image control in your XAML
            Window window = new Window() { Width = 256, Height = 170 };
            window.Content = image;
            window.Show();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    int result = ConvertXYZ.CM_UnInitXYZ(Config.ConvertXYZhandle);
                    ToolBarTop.ClearImageEventHandler -= Clear;
                    ToolBarTop.Dispose();
                    Zoombox1.LayoutUpdated -= Zoombox1_LayoutUpdated;
                    ImageShow.VisualsAdd -= ImageShow_VisualsAdd;
                    ImageShow.VisualsRemove -= ImageShow_VisualsRemove;
                    PreviewKeyDown -= ImageView_PreviewKeyDown;
                    Drop -= ImageView_Drop;
                }
                disposedValue = true;
            }
        }


        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
