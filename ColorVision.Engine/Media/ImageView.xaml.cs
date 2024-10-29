#pragma warning disable CS8625
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Ruler;
using ColorVision.Net;
using ColorVision.UI.Views;
using log4net;
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

    public interface IImageViewComponent
    {
        public void Execute(ImageView imageView);
    }

    public interface IImageViewOpen
    {
        public List<string> Extension { get; }

        public void OpenImage(ImageView imageView, string? filePath);
    }


    public class ImageViewComponentManager
    {
        private static ImageViewComponentManager _instance;
        private static readonly object _locker = new();
        public static ImageViewComponentManager GetInstance() { lock (_locker) { return _instance ??= new ImageViewComponentManager(); } }

        public ObservableCollection<IImageViewComponent> IImageViewComponents { get; set; }
        public ObservableCollection<IImageViewOpen> IImageViewOpens { get; set; }

        public ImageViewComponentManager()
        {
            IImageViewComponents = new ObservableCollection<IImageViewComponent>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(IImageViewComponent).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is IImageViewComponent componentInitialize)
                    {
                        IImageViewComponents.Add(componentInitialize);
                    }
                }
            }
            IImageViewOpens = new ObservableCollection<IImageViewOpen>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(IImageViewOpen).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is IImageViewOpen imageViewOpen)
                    {
                        IImageViewOpens.Add(imageViewOpen);
                    }
                }
            }

        }
    }





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
            foreach (var item in ImageViewComponentManager.GetInstance().IImageViewComponents)
                item.Execute(this);
        }

        public void SetConfig(ImageViewConfig imageViewConfig)
        {
            Config = imageViewConfig;
            this.DataContext = this;
            ToolBarLeft.DataContext = Config;
            ToolBarLayers.DataContext = Config;
            ToolBarTop.OpenProperty = new RelayCommand(a => new DrawProperties(Config) { Owner = Window.GetWindow(Parent), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show());

            var ColormapTypes = PseudoColor.GetColormapDictionary().First(x => x.Key == Config.ColormapTypes);
            string valuepath = ColormapTypes.Value;
            ColormapTypesImage.Source = new BitmapImage(new Uri($"/ColorVision.Engine;component/{valuepath}", UriKind.Relative));
        }

        public ObservableCollection<IDrawingVisual> DrawingVisualLists { get; set; } = new ObservableCollection<IDrawingVisual>();

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ToolBarTop = new ToolBarTop(this,Zoombox1, ImageShow);
            ToolBar1.DataContext = ToolBarTop;
            ToolBarRight.DataContext = ToolBarTop;
            ToolBarBottom.DataContext = ToolBarTop;
            ToolBarTop.ToolBarScaleRuler.ScalRuler.ScaleLocation = ScaleLocation.lowerright;
            ListView1.ItemsSource = DrawingVisualLists;

            ToolBarTop.ClearImageEventHandler += Clear;
            Zoombox1.LayoutUpdated += Zoombox1_LayoutUpdated;
            ImageShow.VisualsAdd += ImageShow_VisualsAdd;
            ImageShow.VisualsRemove += ImageShow_VisualsRemove;
            PreviewKeyDown += ImageView_PreviewKeyDown;
            this.MouseDown += (s, e) => FocusText.Focus();
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
            else if (e.Key == Key.Tab)
            {
                BorderPropertieslayers.Visibility = BorderPropertieslayers.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            }
            else if (e.Key == Key.E)
            {
                ToolBarTop.ImageEditMode = !ToolBarTop.ImageEditMode;
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
            if (Config.IsLayoutUpdated)
            {
                foreach (var item in DrawingVisualLists)
                {
                    item.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                    item.Render();
                }
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

            ToolBarLayers.Visibility = Visibility.Collapsed;
        }

        public void OpenImage(WriteableBitmap? writeableBitmap)
        {
           if (writeableBitmap != null) 
                SetImageSource(writeableBitmap);
        }



        public async void OpenImage(string? filePath)
        {
            log.Info($"OpenImageFile :{filePath}");
            ComboBoxLayers.SelectionChanged -= ComboBoxLayers_SelectionChanged;

            Config.IsCVCIE = false;
            if (filePath != null && File.Exists(filePath))
            {
                long fileSize = new FileInfo(filePath).Length;
                bool isLargeFile = fileSize > 1024 * 1024 * 100;//例如，文件大于1MB时认为是大文件

                string ext = Path.GetExtension(filePath).ToLower(CultureInfo.CurrentCulture);
                var ImageViewOpen = ImageViewComponentManager.GetInstance().IImageViewOpens.FirstOrDefault(a => a.Extension.Any(b => ext.Contains(b)));
                if (ImageViewOpen != null)
                {
                    ImageViewOpen.OpenImage(this, filePath);
                    ComboBoxLayers.SelectedIndex = 0;
                    ComboBoxLayers.SelectionChanged += ComboBoxLayers_SelectionChanged;
                    ToolBarLayers.Visibility = Visibility.Visible;
                    return;
                }
                else
                {

                }


                if (ext.Contains(".cvraw") || ext.Contains(".cvsrc") || ext.Contains(".cvcie"))
                {

                }
                else
                {
                    try
                    {

                        if (Config.IsShowLoadImage && isLargeFile)
                        {
                            WaitControl.Visibility = Visibility.Visible;
                            Config.FilePath = filePath;
                            await Task.Run(() =>
                            {
                                byte[] imageData = File.ReadAllBytes(filePath);
                                BitmapImage bitmapImage = ImageUtils.CreateBitmapImage(imageData);
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    SetImageSource(bitmapImage.ToWriteableBitmap());
                                    WaitControl.Visibility = Visibility.Collapsed;
                                });
                            });

                        }
                        else
                        {
                            Config.FilePath = filePath;
                            BitmapImage bitmapImage = new BitmapImage(new Uri(filePath));
                            SetImageSource(bitmapImage.ToWriteableBitmap());
                        };

                    }
                    catch(Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }

                }
            }


        }

        public HImage? HImageCache { get; set; }

        public void SetImageSource(ImageSource imageSource)
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
            pseudoColor.ShowDialog();
            var ColormapTypes = PseudoColor.GetColormapDictionary().First(x => x.Key == Config.ColormapTypes);
            string valuepath = ColormapTypes.Value;
            ColormapTypesImage.Source = new BitmapImage(new Uri($"/ColorVision.Engine;component/{valuepath}", UriKind.Relative));
            RenderPseudo();
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
                        int ret = OpenCVMediaHelper.M_PseudoColor((HImage)HImageCache, out HImage hImageProcessed, min, max, Config.ColormapTypes);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (ret == 0)
                            {
                                if (!HImageExtension.UpdateWriteableBitmap(PseudoImage , hImageProcessed))
                                {
                                    var image = hImageProcessed.ToWriteableBitmap();
                                    OpenCVMediaHelper.M_FreeHImageData(hImageProcessed.pData);
                                    hImageProcessed.pData = IntPtr.Zero;
                                    PseudoImage = image;
                                }
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


        private void HistogramButton_Click(object sender, RoutedEventArgs e)
        {
            if (ImageShow.Source is not BitmapSource bitmapSource)  return;

            var (redHistogram, greenHistogram, blueHistogram) = ImageUtils.RenderHistogram(bitmapSource);
            if (bitmapSource.Format == PixelFormats.Gray8)
            {
                HistogramChartWindow histogramChartWindow = new HistogramChartWindow(redHistogram);
                histogramChartWindow.Show();
            }
            else
            {
                HistogramChartWindow histogramChartWindow = new HistogramChartWindow(redHistogram, greenHistogram, blueHistogram);
                histogramChartWindow.Show();
            }
        }


        private void ComboBoxLayers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && e.AddedItems[0] is ComboBoxItem comboBoxItem)
            {
                if (Config.IsCVCIE)
                {
                    if (comboBoxItem.Content.ToString() == "Src")
                    {
                        OpenImage(CVFileUtil.OpenLocalFileChannel(Config.FilePath, CVImageChannelType.SRC).ToWriteableBitmap());
                    }
                    if (comboBoxItem.Content.ToString() == "R")
                    {
                        OpenImage(CVFileUtil.OpenLocalFileChannel(Config.FilePath, CVImageChannelType.RGB_R).ToWriteableBitmap());
                    }
                    if (comboBoxItem.Content.ToString() == "G")
                    {
                        OpenImage(CVFileUtil.OpenLocalFileChannel(Config.FilePath, CVImageChannelType.RGB_G).ToWriteableBitmap());

                    }
                    if (comboBoxItem.Content.ToString() == "B")
                    {
                        OpenImage(CVFileUtil.OpenLocalFileChannel(Config.FilePath, CVImageChannelType.RGB_B).ToWriteableBitmap());

                    }
                    if (comboBoxItem.Content.ToString() == "X")
                    {
                        OpenImage(CVFileUtil.OpenLocalFileChannel(Config.FilePath, CVImageChannelType.CIE_XYZ_X).ToWriteableBitmap());
                    }
                    if (comboBoxItem.Content.ToString() == "Y")
                    {
                        OpenImage(CVFileUtil.OpenLocalFileChannel(Config.FilePath, CVImageChannelType.CIE_XYZ_Y).ToWriteableBitmap());

                    }
                    if (comboBoxItem.Content.ToString() == "Z")
                    {
                        OpenImage(CVFileUtil.OpenLocalFileChannel(Config.FilePath, CVImageChannelType.CIE_XYZ_Z).ToWriteableBitmap());
                    }
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
            Task.Run(() =>
            {
                int ret = OpenCVMediaHelper.M_ExtractChannel((HImage)HImageCache, out HImage hImageProcessed, channel);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (ret == 0)
                    {

                        if (!HImageExtension.UpdateWriteableBitmap(PseudoImage, hImageProcessed))
                        {
                            var image = hImageProcessed.ToWriteableBitmap();

                            OpenCVMediaHelper.M_FreeHImageData(hImageProcessed.pData);
                            hImageProcessed.pData = IntPtr.Zero;
                            PseudoImage = image;
                            UpdateZoomAndScale();
                        }
                        ImageShow.Source = PseudoImage;
                        Config.Channel = 1;
                    }
                });
            });

        }

        private void UpdateZoomAndScale()
        {
            Task.Run(() => {
                Application.Current?.Dispatcher.Invoke(() =>
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
                int ret = OpenCVMediaHelper.M_AutoLevelsAdjust((HImage)HImageCache, out HImage hImageProcessed);

                if (ret == 0)
                {
                    if (!HImageExtension.UpdateWriteableBitmap(PseudoImage, hImageProcessed))
                    {
                        var image = hImageProcessed.ToWriteableBitmap();

                        OpenCVMediaHelper.M_FreeHImageData(hImageProcessed.pData);
                        hImageProcessed.pData = IntPtr.Zero;
                        PseudoImage = image;
                        UpdateZoomAndScale();
                    }
                    ImageShow.Source = PseudoImage;
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
                int ret = OpenCVMediaHelper.M_AutomaticColorAdjustment((HImage)HImageCache, out HImage hImageProcessed);
                if (ret == 0)
                {
                    if (!HImageExtension.UpdateWriteableBitmap(PseudoImage, hImageProcessed))
                    {
                        var image = hImageProcessed.ToWriteableBitmap();

                        OpenCVMediaHelper.M_FreeHImageData(hImageProcessed.pData);
                        hImageProcessed.pData = IntPtr.Zero;
                        PseudoImage = image;
                        UpdateZoomAndScale();
                    }
                    ImageShow.Source = PseudoImage;
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
            if (HImageCache == null) return;

            int ret = OpenCVHelper.CM_AutomaticToneAdjustment((HImage)HImageCache, out HImage hImageProcessed);
            if (ret == 0)
            {
                if (!HImageExtension.UpdateWriteableBitmap(PseudoImage, hImageProcessed))
                {
                    var image = hImageProcessed.ToWriteableBitmap();

                    OpenCVMediaHelper.M_FreeHImageData(hImageProcessed.pData);
                    hImageProcessed.pData = IntPtr.Zero;
                    PseudoImage = image;
                    UpdateZoomAndScale();
                }
                ImageShow.Source = PseudoImage;
            }
        }

        private void PseudoSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<HandyControl.Data.DoubleRange> e)
        {
            DebounceTimer.AddOrResetTimer("PseudoSlider", 10, (e) =>
            {
                RenderPseudo();
            }, e.NewValue);
        }

        private void Button_3D_Click(object sender, RoutedEventArgs e)
        {
            if (ImageShow.Source is WriteableBitmap writeableBitmap)
            {
                Window3D window3D = new Window3D(writeableBitmap);
                window3D.Show();
            }
        }

        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
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
