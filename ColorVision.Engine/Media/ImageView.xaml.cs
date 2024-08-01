#pragma warning disable CS8625
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Services.Devices.Algorithm.Templates.POI;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using ColorVision.Net;
using ColorVision.Themes.Controls;
using ColorVision.UI.Draw;
using ColorVision.UI.Draw.Ruler;
using ColorVision.UI.Views;
using ColorVision.Util.Draw.Special;
using cvColorVision;
using CVCommCore.CVAlgorithm;
using CVCommCore.CVImage;
using log4net;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Media
{



    /// <summary>
    /// ImageView.xaml 的交互逻辑
    /// </summary>
    public partial class ImageView : UserControl, IView,IDisposable
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

        private static readonly ILog log = LogManager.GetLogger(typeof(ImageView));

        public ToolBarTop ToolBarTop { get; set; }

        public View View { get; set; }

        public ImageViewConfig Config { get => _Config; set { _Config = value;  } }
        private ImageViewConfig _Config;

        public ImageView()
        {
            Config = new ImageViewConfig();
            View = new View();
            InitializeComponent();
            SetConfig(Config);
        }


        public void SetConfig(ImageViewConfig imageViewConfig)
        {
            Config = imageViewConfig;
            ToolBarLeft.DataContext = Config;
            ToolBarLayers.DataContext = Config;
            ToolBarAl.DataContext = Config;
        }

        public ObservableCollection<IDrawingVisual> DrawingVisualLists { get; set; } = new ObservableCollection<IDrawingVisual>();

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ToolBarTop = new ToolBarTop(this,Zoombox1, ImageShow);
            ToolBar1.DataContext = ToolBarTop;
            ToolBarRight.DataContext = ToolBarTop;
            ToolBarTop.ToolBarScaleRuler.ScalRuler.ScaleLocation = ScaleLocation.lowerright;
            ListView1.ItemsSource = DrawingVisualLists;

            ToolBarTop.ClearImageEventHandler += Clear;
            Zoombox1.LayoutUpdated += Zoombox1_LayoutUpdated;
            ImageShow.VisualsAdd += ImageShow_VisualsAdd;
            ImageShow.VisualsRemove += ImageShow_VisualsRemove;
            PreviewKeyDown += ImageView_PreviewKeyDown;
            this.MouseDown += (s, e) => FocusText.Focus();
            Drop += ImageView_Drop;

            if (PoiParam.Params.Count == 0)
            {
                MySqlControl.GetInstance().Connect();
                new TemplatePOI().Load();

            }

            if (MySqlControl.GetInstance().IsConnect)
            {
                ComboxPOITemplate.ItemsSource = PoiParam.Params.CreateEmpty();
                ComboxPOITemplate.SelectedIndex = 0;
                ToolBarAl.Visibility = Visibility.Visible;
            }
            else
            {
                Task.Run(()=> LoadMysql());
            }

        }

        public async Task LoadMysql()
        {
            await Task.Delay(100);
            await MySqlControl.GetInstance().Connect();
            if (MySqlControl.GetInstance().IsConnect)
            {
                new TemplatePOI().Load();
                ComboxPOITemplate.ItemsSource = PoiParam.Params.CreateEmpty();
                ComboxPOITemplate.SelectedIndex = 0;
                ToolBarAl.Visibility = Visibility.Visible;
            }
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
                    OpenImage(fn);
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
        private DVCircle? SelectDCircle;
        private DVRectangle? SelectRectangle;
        private DVCircle DrawCircleCache;
        private DVRectangle DrawingRectangleCache;
        private DVPolygon? DrawingVisualPolygonCache;


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
                MenuItem menuIte2 = new() { Header = Properties.Resources.MenuDelete };
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
                    DrawCircleCache = new DVCircle() { AutoAttributeChanged = false };
                    DrawCircleCache.Attribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                    DrawCircleCache.Attribute.Center = MouseDownP;
                    drawCanvas.AddVisual(DrawCircleCache);
                }
                else if (ToolBarTop.DrawRect)
                {
                    DrawingRectangleCache = new DVRectangle() { AutoAttributeChanged = false };
                    DrawingRectangleCache.Attribute.Rect = new Rect(MouseDownP, new Point(MouseDownP.X + 30, MouseDownP.Y + 30));
                    DrawingRectangleCache.Attribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);

                    drawCanvas.AddVisual(DrawingRectangleCache);
                }
                else if (ToolBarTop.DrawPolygon)
                {
                    if (DrawingVisualPolygonCache == null)
                    {
                        DrawingVisualPolygonCache = new DVPolygon();
                        DrawingVisualPolygonCache.Attribute.Pen.Thickness = 1 / Zoombox1.ContentMatrix.M11;
                        drawCanvas.AddVisual(DrawingVisualPolygonCache);
                    }
                }
                else
                {
                    if (drawCanvas.GetVisual(MouseDownP) is IDrawingVisual drawingVisual)
                    {
                        if (PropertyGrid2.SelectedObject is BaseProperties viewModelBase)
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
                            if (drawingVisual is DVRectangle Rectangle)
                            {
                                SelectRectangle = Rectangle;
                            }
                            else if (drawingVisual is DVCircle Circl)
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

            ToolBarLayers.Visibility = Visibility.Collapsed;
        }

        public void OpenImage(WriteableBitmap? writeableBitmap)
        {
           if (writeableBitmap != null) 
                SetImageSource(writeableBitmap);
        }

        public void CVCIESetBuffer(string filePath)
        {
            if (!Config.ConvertXYZhandleOnce)
            {
                int result = ConvertXYZ.CM_InitXYZ(Config.ConvertXYZhandle);
                log.Info($"ConvertXYZ.CM_InitXYZ :{result}");
                Config.ConvertXYZhandleOnce = true;
            }
            Config.FilePath = filePath;
            if (File.Exists(filePath) && CVFileUtil.IsCIEFile(filePath))
            {
                int index = CVFileUtil.ReadCIEFileHeader(Config.FilePath, out CVCIEFile meta);
                if (meta.FileExtType == FileExtType.CIE)
                {
                    Config.IsCVCIE = true;
                    CVFileUtil.ReadCIEFileData(Config.FilePath, ref meta, index);
                    int resultCM_SetBufferXYZ = ConvertXYZ.CM_SetBufferXYZ(Config.ConvertXYZhandle, (uint)meta.rows, (uint)meta.cols, (uint)meta.bpp, (uint)meta.channels, meta.data);
                    log.Debug($"CM_SetBufferXYZ :{resultCM_SetBufferXYZ}");
                    ToolBarTop.MouseMagnifier.MouseMoveColorHandler -= ShowCVCIE;
                    ToolBarTop.MouseMagnifier.MouseMoveColorHandler += ShowCVCIE;
                }
            }
        }

        public void OpenImage(string? filePath)
        {
            log.Info($"OpenImageFile :{filePath}");
            ComboBoxLayers.SelectionChanged -= ComboBoxLayers_SelectionChanged;

            ToolBarTop.MouseMagnifier.MouseMoveColorHandler -= ShowCVCIE;
            Config.IsCVCIE = false;
            if (filePath != null && File.Exists(filePath))
            {
                long fileSize = new FileInfo(filePath).Length;
                bool isLargeFile = fileSize > 1024 * 1024 * 100;//例如，文件大于1MB时认为是大文件

                string ext = Path.GetExtension(filePath).ToLower(CultureInfo.CurrentCulture);
                if (ext.Contains(".cvraw") || ext.Contains(".cvsrc") || ext.Contains(".cvcie"))
                {
                    FileExtType fileExtType = ext.Contains(".cvraw") ? FileExtType.Raw : ext.Contains(".cvsrc") ? FileExtType.Src : FileExtType.CIE;
                    CVCIESetBuffer(filePath);
                    try
                    {
                        if (Config.IsShowLoadImage && isLargeFile)
                        {  
                            WaitControl.Visibility = Visibility.Visible;
                            Task.Run(() =>
                            {
                                CVCIEFile cVCIEFile = new NetFileUtil().OpenLocalCVFile(filePath, fileExtType);
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

            ComboBoxLayers.SelectedIndex = 0;
            ComboBoxLayers.SelectionChanged += ComboBoxLayers_SelectionChanged;
            ToolBarLayers.Visibility = Visibility.Visible;
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

        private void SetImageSource(ImageSource imageSource)
        {
            if (HImageCache != null)
            {
                HImageCache?.Dispose();
                HImageCache = null;
            };

            if (imageSource is WriteableBitmap writeableBitmap)
            {
                Task.Run(() => Application.Current.Dispatcher.Invoke((() =>
                {
                    HImageCache = writeableBitmap.ToHImage();
                    if (HImageCache is HImage hImage)
                    {
                        Config.Channel = hImage.channels;
                        Config.Ochannel = Config.Channel;
                    }
                })));
            }

            if (imageSource is BitmapImage bitmapImage)
            {
                Task.Run(() => Application.Current.Dispatcher.Invoke((() =>
                {
                    HImageCache = bitmapImage.ToHImage();
                    if (HImageCache is HImage hImage)
                    {
                        Config.Channel = hImage.channels;
                        Config.Ochannel = Config.Channel;
                    }
                })));
            }

            ViewBitmapSource = imageSource;
            ImageShow.Source = ViewBitmapSource;

            UpdateZoomAndScale();
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

                    log.Info($"ImagePath，正在执行PseudoColor,min:{min},max:{max}");
                    Task.Run(() =>
                    {
                        int ret = OpenCVHelper.CM_PseudoColor((HImage)HImageCache, out HImage hImageProcessed, min, max, Config.ColormapTypes);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (ret == 0)
                            {
                                var image = hImageProcessed.ToWriteableBitmap();
                                OpenCVHelper.FreeHImageData(hImageProcessed.pData);
                                hImageProcessed.pData = IntPtr.Zero;

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

        private bool disposedValue;

        public void ShowCVCIE(object sender, ImageInfo imageInfo)
        {
            float dXVal = 0;
            float dYVal = 0;
            float dZVal = 0;
            float dx = 0, dy = 0, du = 0, dv = 0;
            _= ConvertXYZ.CM_GetXYZxyuvRect(Config.ConvertXYZhandle, imageInfo.X, imageInfo.Y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, DefalutTextAttribute.Defalut.CVCIENum, DefalutTextAttribute.Defalut.CVCIENum);
            ToolBarTop.MouseMagnifier.DrawImageCVCIE(imageInfo, dXVal, dYVal, dZVal, dx, dy, du, dv);
        }


        private WindowCIE windowCIE;

        private void ButtonCIE1931_Click(object sender, RoutedEventArgs e)
        {
            bool old = ToolBarTop.ShowImageInfo;
            ToolBarTop.ShowImageInfo = true;

            if (windowCIE == null)
            {
                windowCIE = new WindowCIE() { Owner = Application.Current.GetActiveWindow() };
                void mouseMoveColorHandler(object s, ImageInfo e)
                {
                    if (Config.IsCVCIE)
                    {
                        int xx = e.X;
                        int yy = e.Y;
                        float dXVal = 0;
                        float dYVal = 0;
                        float dZVal = 0;
                        float dx = 0, dy = 0, du = 0, dv = 0;
                        int result = ConvertXYZ.CM_GetXYZxyuvRect(Config.ConvertXYZhandle, xx, yy, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, DefalutTextAttribute.Defalut.CVCIENum, DefalutTextAttribute.Defalut.CVCIENum);
                        log.Debug($"CM_GetXYZxyuvRect :{result},res");
                        windowCIE.ChangeSelect(dx, dy);
                    }
                    else
                    {
                        windowCIE.ChangeSelect(e);
                    }
                }

                ToolBarTop.MouseMagnifier.MouseMoveColorHandler += mouseMoveColorHandler;
                windowCIE.Closed += (s, e) =>
                {
                    ToolBarTop.MouseMagnifier.MouseMoveColorHandler -= mouseMoveColorHandler;
                    ToolBarTop.ShowImageInfo = old;
                    windowCIE = null;
                };
            }
            windowCIE.Show();
            windowCIE.Activate();
        }


        private void HistogramButton_Click(object sender, RoutedEventArgs e)
        {
            if (ImageShow.Source is not BitmapSource bitmapSource)  return;

            var bmp= ImageUtils.RenderHistogram(bitmapSource);
            Image image = new Image() { Margin = new Thickness(5) };
            image.Source = bmp;
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

        private void ComboBoxLayers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && e.AddedItems[0] is ComboBoxItem comboBoxItem)
            {
                if (Config.IsCVCIE)
                {
                    string ext = Path.GetExtension(Config.FilePath)?.ToLower(CultureInfo.CurrentCulture);
                    if (string.IsNullOrEmpty(ext)) return;
                    FileExtType fileExtType = ext.Contains(".cvraw") ? FileExtType.Raw : ext.Contains(".cvsrc") ? FileExtType.Src : FileExtType.CIE;

                    if (comboBoxItem.Content.ToString() == "Src")
                        OpenImage(CVFileUtil.OpenLocalFileChannel(Config.FilePath, fileExtType, CVImageChannelType.SRC).ToWriteableBitmap());
                    if (comboBoxItem.Content.ToString() == "R")
                        OpenImage(CVFileUtil.OpenLocalFileChannel(Config.FilePath, fileExtType, CVImageChannelType.RGB_R).ToWriteableBitmap());
                    if (comboBoxItem.Content.ToString() == "G")
                        OpenImage(CVFileUtil.OpenLocalFileChannel(Config.FilePath, fileExtType, CVImageChannelType.RGB_G).ToWriteableBitmap());
                    if (comboBoxItem.Content.ToString() == "B")
                        OpenImage(CVFileUtil.OpenLocalFileChannel(Config.FilePath, fileExtType, CVImageChannelType.RGB_B).ToWriteableBitmap());
                    if (comboBoxItem.Content.ToString() == "X")
                        OpenImage(CVFileUtil.OpenLocalFileChannel(Config.FilePath, fileExtType, CVImageChannelType.CIE_XYZ_X).ToWriteableBitmap());
                    if (comboBoxItem.Content.ToString() == "Y")
                        OpenImage(CVFileUtil.OpenLocalFileChannel(Config.FilePath, fileExtType, CVImageChannelType.CIE_XYZ_Y).ToWriteableBitmap());
                    if (comboBoxItem.Content.ToString() == "Z")
                        OpenImage(CVFileUtil.OpenLocalFileChannel(Config.FilePath, fileExtType, CVImageChannelType.CIE_XYZ_Z).ToWriteableBitmap());
                }
                else
                {
                    if (comboBoxItem.Content.ToString() == "Src")
                        CM_ExtractChannel(-1);
                    if (comboBoxItem.Content.ToString() == "R")
                        CM_ExtractChannel(2);
                    if (comboBoxItem.Content.ToString() == "G")
                        CM_ExtractChannel(1);
                    if (comboBoxItem.Content.ToString() == "B")
                        CM_ExtractChannel(0);

                }

            }
        }

        private void CM_ExtractChannel(int channel)
        {
            if (ViewBitmapSource == null) return;

            if (channel == -1)
            {
                ImageShow.Source = ViewBitmapSource;
                UpdateZoomAndScale();
                Config.Channel = Config.Ochannel;
                return;
            }
            if (HImageCache == null) return;

            int ret = OpenCVHelper.CM_ExtractChannel((HImage)HImageCache, out HImage hImageProcessed, channel);
            if (ret == 0)
            {
                var image = hImageProcessed.ToWriteableBitmap();
                OpenCVHelper.FreeHImageData(hImageProcessed.pData);
                hImageProcessed.pData = IntPtr.Zero;
                PseudoImage = image;
                ImageShow.Source = PseudoImage;
                Config.Channel = 1;
                UpdateZoomAndScale();
            }
        }

        private void UpdateZoomAndScale()
        {
            Task.Run(() => {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Zoombox1.ZoomUniform();
                    ToolBarTop.ToolBarScaleRuler.Render();
                });
            });
        }

        private void CM_AutoLevelsAdjust(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton toggleButton) return;
            if (toggleButton.IsChecked == false)
            {
                ImageShow.Source = ViewBitmapSource;
                PseudoImage = null;
                return;
            }
            if (HImageCache != null)
            {
                int ret = OpenCVHelper.CM_AutoLevelsAdjust((HImage)HImageCache, out HImage hImageProcessed);

                if (ret == 0)
                {
                    var image = hImageProcessed.ToWriteableBitmap();
                    OpenCVHelper.FreeHImageData(hImageProcessed.pData);
                    hImageProcessed.pData = IntPtr.Zero;

                    PseudoImage = image;
                    if (toggleButton.IsChecked == true)
                    {
                        ImageShow.Source = PseudoImage;
                    }
                }
            };
        }

        private void CM_AutomaticColorAdjustment(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton toggleButton) return;
            if (toggleButton.IsChecked == false)
            {
                ImageShow.Source = ViewBitmapSource;
                PseudoImage = null;
                return;
            }
            if (HImageCache != null)
            {
                int ret = OpenCVHelper.CM_AutomaticColorAdjustment((HImage)HImageCache, out HImage hImageProcessed);
                if (ret == 0)
                {
                    var image = hImageProcessed.ToWriteableBitmap();
                    OpenCVHelper.FreeHImageData(hImageProcessed.pData);
                    hImageProcessed.pData = IntPtr.Zero;

                    PseudoImage = image;
                    if (toggleButton.IsChecked == true)
                    {
                        ImageShow.Source = PseudoImage;
                    }
                }
            };
        }

        private void CM_AutomaticToneAdjustment(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton toggleButton) return;
            if (toggleButton.IsChecked == false)
            {
                ImageShow.Source = ViewBitmapSource;
                PseudoImage = null;
                return;
            }
            if (HImageCache != null)
            {
                int ret = OpenCVHelper.CM_AutomaticToneAdjustment((HImage)HImageCache, out HImage hImageProcessed);
                if (ret == 0)
                {
                    var image = hImageProcessed.ToWriteableBitmap();
                    OpenCVHelper.FreeHImageData(hImageProcessed.pData);
                    hImageProcessed.pData = IntPtr.Zero;

                    PseudoImage = image;
                    if (toggleButton.IsChecked == true)
                    {
                        ImageShow.Source = PseudoImage;
                    }
                }
            };
        }

        private void PseudoSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<HandyControl.Data.DoubleRange> e)
        {
            DebounceTimer.AddOrResetTimer("PseudoSlider", 50, (e) =>
            {
                RenderPseudo();
            }, e.NewValue);
        }

        private void CalculPOI_Click(object sender, RoutedEventArgs e)
        {
            if (!Config.IsCVCIE)
            {
                MessageBox1.Show("仅对CVCIE图像支持");
                return;
            }
            if (ComboxPOITemplate.SelectedValue is not PoiParam poiParams)
            {
                MessageBox1.Show("需要配置关注点");
                return;
            }


            ObservableCollection<PoiResultCIExyuvData> PoiResultCIExyuvDatas = new ObservableCollection<PoiResultCIExyuvData>();
            int result = ConvertXYZ.CM_SetFilter(Config.ConvertXYZhandle, poiParams.DatumArea.Filter.Enable, poiParams.DatumArea.Filter.Threshold);
            result = ConvertXYZ.CM_SetFilterNoArea(Config.ConvertXYZhandle, poiParams.DatumArea.Filter.NoAreaEnable, poiParams.DatumArea.Filter.Threshold);
            result = ConvertXYZ.CM_SetFilterXYZ(Config.ConvertXYZhandle, poiParams.DatumArea.Filter.XYZEnable, (int)poiParams.DatumArea.Filter.XYZType, poiParams.DatumArea.Filter.Threshold);

            poiParams.PoiPoints.Clear();
            foreach (var item in DrawingVisualLists)
            {
                BaseProperties drawAttributeBase = item.BaseAttribute;
                if (drawAttributeBase is CircleTextProperties circle)
                {
                    PoiPoint poiParamData = new PoiPoint()
                    {
                        PointType = RiPointTypes.Circle,
                        PixX = circle.Center.X,
                        PixY = circle.Center.Y,
                        PixWidth = circle.Radius * 2,
                        PixHeight = circle.Radius * 2,
                        Tag = circle.Tag,
                        Name = circle.Text
                    };
                    poiParams.PoiPoints.Add(poiParamData);
                }
                else if (drawAttributeBase is CircleProperties circleProperties)
                {
                    PoiPoint poiParamData = new PoiPoint()
                    {
                        PointType = RiPointTypes.Circle,
                        PixX = circleProperties.Center.X,
                        PixY = circleProperties.Center.Y,
                        PixWidth = circleProperties.Radius * 2,
                        PixHeight = circleProperties.Radius * 2,
                        Tag = circleProperties.Tag,
                        Name = circleProperties.Id.ToString()
                    };
                    poiParams.PoiPoints.Add(poiParamData);
                }
                else if (drawAttributeBase is RectangleTextProperties rectangle)
                {
                    PoiPoint poiParamData = new()
                    {
                        Name = rectangle.Text,
                        PointType = RiPointTypes.Rect,
                        PixX = rectangle.Rect.X + rectangle.Rect.Width / 2,
                        PixY = rectangle.Rect.Y + rectangle.Rect.Height / 2,
                        PixWidth = rectangle.Rect.Width,
                        PixHeight = rectangle.Rect.Height,
                        Tag = rectangle.Tag,
                    };
                    poiParams.PoiPoints.Add(poiParamData);
                }
                else if (drawAttributeBase is RectangleProperties rectangleProperties)
                {
                    PoiPoint poiParamData = new PoiPoint()
                    {
                        PointType = RiPointTypes.Rect,
                        PixX = rectangleProperties.Rect.X + rectangleProperties.Rect.Width / 2,
                        PixY = rectangleProperties.Rect.Y + rectangleProperties.Rect.Height / 2,
                        PixWidth = rectangleProperties.Rect.Width,
                        PixHeight = rectangleProperties.Rect.Height,
                        Tag = rectangleProperties.Tag,
                    };
                    poiParams.PoiPoints.Add(poiParamData);
                }
            }



            foreach (var item in poiParams.PoiPoints)
            {
                POIPoint pOIPoint = new POIPoint() { Id = item.Id, Name = item.Name, PixelX = (int)item.PixX, PixelY = (int)item.PixY, PointType = (POIPointTypes)item.PointType, Height = (int)item.PixHeight, Width = (int)item.PixWidth };
                var sss = GetCVCIE(pOIPoint);
                PoiResultCIExyuvDatas.Add(sss);
            }


            WindowCVCIE windowCIE = new WindowCVCIE(PoiResultCIExyuvDatas) { Owner = Application.Current.GetActiveWindow() };
            windowCIE.Show();
        }


        public PoiResultCIExyuvData GetCVCIE(POIPoint pOIPoint)
        {
            int x = pOIPoint.PixelX; int y = pOIPoint.PixelY; int rect = pOIPoint.Width; int rect2 = pOIPoint.Height;
            PoiResultCIExyuvData poiResultCIExyuvData = new PoiResultCIExyuvData();
            poiResultCIExyuvData.Point = pOIPoint;
            float dXVal = 0;
            float dYVal = 0;
            float dZVal = 0;
            float dx = 0;
            float dy = 0;
            float du = 0;
            float dv = 0;
            float CCT = 0;
            float Wave = 0;

            switch (pOIPoint.PointType)
            {
                case POIPointTypes.None:
                    break;
                case POIPointTypes.SolidPoint:
                    _ = ConvertXYZ.CM_GetXYZxyuvCircle(Config.ConvertXYZhandle, x, y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, 1);
                    break;
                case POIPointTypes.Circle:
                    _ = ConvertXYZ.CM_GetXYZxyuvCircle(Config.ConvertXYZhandle, x, y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, (int)(rect / 2));
                    break;
                case POIPointTypes.Rect:
                     _ = ConvertXYZ.CM_GetXYZxyuvRect(Config.ConvertXYZhandle, x, y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, rect, rect2);
                    break;
                case POIPointTypes.Mask:
                    break;
                default:
                    break;
            }

            poiResultCIExyuvData.u = du;
            poiResultCIExyuvData.v = dv;
            poiResultCIExyuvData.x = dx;
            poiResultCIExyuvData.y = dy;
            poiResultCIExyuvData.X = dXVal;
            poiResultCIExyuvData.Y = dYVal;
            poiResultCIExyuvData.Z = dZVal;

            int i = ConvertXYZ.CM_GetxyuvCCTWaveCircle(Config.ConvertXYZhandle, x, y, ref dx, ref dy, ref du, ref dv, ref CCT, ref Wave, (int)(rect / 2));
            poiResultCIExyuvData.CCT = CCT;
            poiResultCIExyuvData.Wave = Wave;

            return poiResultCIExyuvData;
        }

        private void ComboxPOITemplate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedValue is PoiParam poiParams)
            {
                ImageShow.Clear();
                DrawingVisualLists.Clear();

                if (poiParams.Id == -1) return;

                PoiParam.LoadPoiDetailFromDB(poiParams);
                foreach (var item in poiParams.PoiPoints)
                {
                    switch (item.PointType)
                    {
                        case RiPointTypes.Circle:
                            DVCircleText Circle = new();
                            Circle.Attribute.Center = new Point(item.PixX, item.PixY);
                            Circle.Attribute.Radius = item.PixHeight / 2;
                            Circle.Attribute.Brush = Brushes.Transparent;
                            Circle.Attribute.Pen = new Pen(Brushes.Red, item.PixWidth / 30);
                            Circle.Attribute.Id = item.Id;
                            Circle.Attribute.Text = item.Name;
                            Circle.Render();
                            ImageShow.AddVisual(Circle);
                            break;
                        case RiPointTypes.Rect:
                            DVRectangleText Rectangle = new();
                            Rectangle.Attribute.Rect = new Rect(item.PixX - item.PixWidth / 2, item.PixY - item.PixHeight / 2, item.PixWidth, item.PixHeight);
                            Rectangle.Attribute.Brush = Brushes.Transparent;
                            Rectangle.Attribute.Pen = new Pen(Brushes.Red, item.PixWidth / 30);
                            Rectangle.Attribute.Id = item.Id;
                            Rectangle.Attribute.Text = item.Name;
                            Rectangle.Render();
                            ImageShow.AddVisual(Rectangle);
                            break;
                        case RiPointTypes.Mask:
                            break;
                    }
                }
            }

        }

        private void Button_3D_Click(object sender, RoutedEventArgs e)
        {
            if (ImageShow.Source is WriteableBitmap writeableBitmap)
            {
                Window3D window3D = new Window3D(writeableBitmap);
                window3D.Show();
            }

        }
    }
}
