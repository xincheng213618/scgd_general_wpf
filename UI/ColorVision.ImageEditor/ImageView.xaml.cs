#pragma warning disable CS8625
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Ruler;
using ColorVision.UI.Menus;
using ColorVision.UI.Views;
using Gu.Wpf.Geometry;
using log4net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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

namespace ColorVision.ImageEditor
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

        public ImageViewModel ImageViewModel { get; set; }

        public View View { get; set; }

        public ImageViewConfig Config { get => _Config; set { _Config = value;  } }
        private ImageViewConfig _Config;

        public ImageView()
        {
            Config = new ImageViewConfig();
            View = new View();
            InitializeComponent();
            SetConfig(Config);
            foreach (var item in ImageComponentManager.GetInstance().IImageComponents)
                item.Execute(this);
        }

        public void Open()
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                OpenImage(openFileDialog.FileName);
            }
        }


        public void SetConfig(ImageViewConfig imageViewConfig)
        {
            if (Config != null)
            {
                Config.ColormapTypesChanged -= Config_ColormapTypesChanged;
                Config.BalanceChanged -= ImageViewConfig_BalanceChanged;
            }
            Config = imageViewConfig;
            this.DataContext = this;
            ToolBarLeft.DataContext = Config;
            Zoombox1.DataContext = imageViewConfig;
            ImageViewModel.PropertyCommand = new RelayCommand(a => new DrawProperties(Config) { Owner = Window.GetWindow(Parent), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show());

            Config.ColormapTypesChanged += Config_ColormapTypesChanged;
            Config.BalanceChanged += ImageViewConfig_BalanceChanged;
        }

        private void Config_ColormapTypesChanged(object? sender, EventArgs e)
        {
            var ColormapTypes = PseudoColor.GetColormapDictionary().First(x => x.Key == Config.ColormapTypes);
            string valuepath = ColormapTypes.Value;
            ColormapTypesImage.Source = new BitmapImage(new Uri($"/ColorVision.ImageEditor;component/{valuepath}", UriKind.Relative));
        }

        private void ImageViewConfig_BalanceChanged(object? sender, EventArgs e)
        {
            Common.Utilities.DebounceTimer.AddOrResetTimer("AdjustWhiteBalance", 30, AdjustWhiteBalance);
        }

        public ObservableCollection<IDrawingVisual> DrawingVisualLists { get; set; } = new ObservableCollection<IDrawingVisual>();

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ImageViewModel = new ImageViewModel(this,Zoombox1, ImageShow);
            Zoombox1.ContextMenu = ImageViewModel.ContextMenu;
            ToolBar1.DataContext = ImageViewModel;
            ToolBarRight.DataContext = ImageViewModel;
            ToolBarBottom.DataContext = ImageViewModel;
            ImageViewModel.ToolBarScaleRuler.ScalRuler.ScaleLocation = ScaleLocation.lowerright;
            ImageViewModel.ClearImageEventHandler += Clear;
            ImageViewModel.OpenImageEventHandler += (s, e) => OpenImage(e);
            Zoombox1.LayoutUpdated += Zoombox1_LayoutUpdated;
            ImageShow.VisualsAdd += ImageShow_VisualsAdd;
            ImageShow.VisualsRemove += ImageShow_VisualsRemove;
            PreviewKeyDown += ImageView_PreviewKeyDown;
            Drop += ImageView_Drop;

            ComColormapTypes.ItemsSource = PseudoColor.GetColormapsDictionary();
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
                InfoText.Text = Config.GetPropertyString();
            }
            else if (e.Key == Key.E)
            {
                ImageViewModel.ImageEditMode = !ImageViewModel.ImageEditMode;
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
                double scale = 1/ Zoombox1.ContentMatrix.M11;
                DebounceTimer.AddOrResetTimerDispatcher("ImageLayoutUpdatedRender", 20, ()=>ImageLayoutUpdatedRender(scale));
            }
        }

        public void ImageLayoutUpdatedRender(double scale)
        {
            foreach (var item in DrawingVisualLists)
            {
                item.Pen.Thickness = scale;
                item.Render();
            }
        }

        private DrawingVisual SelectRect = new DrawingVisual();
        private DrawingVisual SelectRect1 = new DrawingVisual();

        private bool IsMouseDown;
        private Point MouseDownP;

        private DVCircle DrawCircleCache;
        private DVRectangle DrawingRectangleCache;
        private DVPolygon? DrawingVisualPolygonCache;


        private void ImageShow_Initialized(object sender, EventArgs e)
        {
            ImageShow.ContextMenuOpening += MainWindow_ContextMenuOpening;
        }

        private void MainWindow_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (ImageViewModel.ImageEditMode)
            {
                var Point = Mouse.GetPosition(ImageShow);
                var DrawingVisual = ImageShow.GetVisual(Point);

                if (DrawingVisual != null && ImageViewModel.SelectDrawingVisual != DrawingVisual && DrawingVisual is IDrawingVisual drawing)
                {
                    Zoombox1.ContextMenu ??= new ContextMenu();
                    Zoombox1.ContextMenu.Items.Clear();
                    var ContextMenu = Zoombox1.ContextMenu;
                    MenuItem menuItem = new() { Header = "隐藏(_H)" };
                    menuItem.Click += (s, e) =>
                    {
                        drawing.BaseAttribute.IsShow = false;
                    };
                    MenuItem menuIte2 = new() { Header = "删除" };
                    menuIte2.Click += (s, e) =>
                    {
                        ImageShow.RemoveVisual(DrawingVisual);
                        PropertyGrid2.SelectedObject = null;
                    };
                    ContextMenu.Items.Add(menuItem);
                    ContextMenu.Items.Add(menuIte2);
                }
                else
                {
                    Zoombox1.ContextMenu.Items.Clear();
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

        bool IsRightButtonDown = false;

        private void ImageShow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DrawingVisualPolygonCache != null)
            {
                DrawingVisualPolygonCache.MovePoints = null;
                DrawingVisualPolygonCache.Render();
                DrawingVisualPolygonCache = null;
            }
            IsRightButtonDown = true;
        }


        public void SelectDrawingVisualsClear()
        {
            if (ImageViewModel.SelectDrawingVisuals != null)
            {
                foreach (var item in ImageViewModel.SelectDrawingVisuals)
                {
                    if (item is IDrawingVisual id)
                    {
                        id.Pen.Brush = Brushes.Red;
                        id.Render();
                    }
                }
                ImageViewModel.SelectDrawingVisuals = null;
            }
        }

        public void SelectDrawingVisualClear()
        {
            if (ImageViewModel.SelectDrawingVisual != null)
            {
                if (ImageViewModel.SelectDrawingVisual is IDrawingVisual id)
                {
                    id.Pen.Brush = Brushes.Red;
                    id.Render();
                }
                ImageViewModel.SelectDrawingVisual = null;
            }
        }

        private void ImageShow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas && !Keyboard.Modifiers.HasFlag(Zoombox1.ActivateOn))
            {
                if (ImageViewModel.Gridline.IsShow == true)
                    return;
                if (ImageViewModel.ConcentricCircle == true)
                    return;

                drawCanvas.CaptureMouse();
                MouseDownP = e.GetPosition(drawCanvas);
                IsMouseDown = true;

                if (ImageViewModel.EraseVisual)
                {
                    ImageViewModel.DrawSelectRect(SelectRect, new Rect(MouseDownP, MouseDownP)); ;
                    drawCanvas.AddVisual(SelectRect,false);

                    if (ImageViewModel.SelectDrawingVisuals != null)
                    {
                        foreach (var item in ImageViewModel.SelectDrawingVisuals)
                        {
                            if (item is IDrawingVisual id)
                            {
                                id.Pen.Brush = Brushes.Red;
                                id.Render();
                            }
                        }
                        ImageViewModel.SelectDrawingVisuals = null;
                    }
                    return;
                }

                if (ImageViewModel.DrawCircle)
                {
                    DrawCircleCache = new DVCircle() { AutoAttributeChanged = false };
                    DrawCircleCache.Attribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);
                    DrawCircleCache.Attribute.Center = MouseDownP;
                    DrawCircleCache.Attribute.Radius = DefalutRadius;
                    drawCanvas.AddVisual(DrawCircleCache);

                    SelectDrawingVisualClear();
                    SelectDrawingVisualsClear();
                    return;
                }

                if (ImageViewModel.DrawRect)
                {
                    DrawingRectangleCache = new DVRectangle() { AutoAttributeChanged = false };
                    DrawingRectangleCache.Attribute.Rect = new Rect(MouseDownP, new Point(MouseDownP.X + DefalutWidth, MouseDownP.Y + DefalutHeight));
                    DrawingRectangleCache.Attribute.Pen = new Pen(Brushes.Red, 1 / Zoombox1.ContentMatrix.M11);

                    drawCanvas.AddVisual(DrawingRectangleCache);

                    SelectDrawingVisualClear();
                    SelectDrawingVisualsClear();
                    return;
                }

                if (ImageViewModel.DrawPolygon)
                {
                    if (DrawingVisualPolygonCache == null)
                    {
                        DrawingVisualPolygonCache = new DVPolygon();
                        DrawingVisualPolygonCache.Attribute.Pen.Thickness = 1 / Zoombox1.ContentMatrix.M11;
                        drawCanvas.AddVisual(DrawingVisualPolygonCache);
                    }

                    SelectDrawingVisualClear();
                    SelectDrawingVisualsClear();

                    return;
                }


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

                    if (ImageViewModel.ImageEditMode == true)
                    {
                        if (ImageViewModel.SelectDrawingVisuals != null && drawingVisual is DrawingVisual visual1 && ImageViewModel.SelectDrawingVisuals.Contains(visual1))
                            return;



                        if (drawingVisual is DrawingVisual visual)
                        {
                            if (ImageViewModel.SelectDrawingVisual != visual)
                            {
                                if (ImageViewModel.SelectDrawingVisual is IDrawingVisual id)
                                {
                                    id.Pen.Brush = Brushes.Red;
                                    id.Render();
                                }
                                ImageViewModel.SelectDrawingVisual = null;
                            }
                            ImageViewModel.SelectDrawingVisual = visual;
                            drawingVisual.Pen.Brush = Brushes.Yellow;
                            drawingVisual.Render();

                            //if (!drawCanvas.ContainsVisual(SelectRect1))
                            //{
                            //    drawCanvas.AddVisual(SelectRect1,false);
                            //}
                            //ImageViewModel.DrawSelectRect(SelectRect1, VisualTreeHelper.GetDescendantBounds(visual));
                        }

                        if (ImageViewModel.SelectDrawingVisual is DVCircle Circl)
                        {
                            Circl.IsDrawing = true;
                        }
                        if (ImageViewModel.SelectDrawingVisuals != null)
                        {
                            foreach (var item in ImageViewModel.SelectDrawingVisuals)
                            {
                                if (item is IDrawingVisual id)
                                {
                                    id.Pen.Brush = Brushes.Red;
                                    id.Render();
                                }
                            }
                            ImageViewModel.SelectDrawingVisuals = null;
                        }
                    }
                    return;
                }
                SelectDrawingVisualClear();
                SelectDrawingVisualsClear();
                ImageViewModel.DrawSelectRect(SelectRect, new Rect(MouseDownP, MouseDownP)); ;
                drawCanvas.AddVisual(SelectRect, false);

            }
        }


        Point LastMouseMove;
        private void ImageShow_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas && (Zoombox1.ActivateOn == ModifierKeys.None || !Keyboard.Modifiers.HasFlag(Zoombox1.ActivateOn)))
            {
                var point = e.GetPosition(drawCanvas);
                if (ImageViewModel.DrawPolygon)
                {
                    if (DrawingVisualPolygonCache != null)
                    {
                        DrawingVisualPolygonCache.MovePoints = point;
                        DrawingVisualPolygonCache.Render();
                    }
                }

                if (IsMouseDown)
                {
                    ImageViewModel.DrawSelectRect(SelectRect, new Rect(MouseDownP, point));

                    if (ImageViewModel.DrawCircle)
                    {
                        if (DrawCircleCache != null)
                        {
                            double Radius = Math.Sqrt((Math.Pow(point.X - MouseDownP.X, 2) + Math.Pow(point.Y - MouseDownP.Y, 2)));
                            DrawCircleCache.Attribute.Radius = Radius;
                            DrawCircleCache.Render();
                        }
                    }
                    else if (ImageViewModel.DrawRect)
                    {
                        if (DrawingRectangleCache != null)
                        {
                            DrawingRectangleCache.Attribute.Rect = new Rect(MouseDownP, point);
                            DrawingRectangleCache.Render();
                        }
                    }

                    if (ImageViewModel.SelectDrawingVisual != null)
                    {
                        if (ImageViewModel.SelectDrawingVisual is IRectangle rectangle)
                        {
                            var OldRect = rectangle.Rect;
                            rectangle.Rect = new Rect(OldRect.X + point.X - LastMouseMove.X, OldRect.Y + point.Y - LastMouseMove.Y, OldRect.Width, OldRect.Height);
                        }
                        else if (ImageViewModel.SelectDrawingVisual is ICircle Circl)
                        {
                            Circl.Center += point - LastMouseMove;
                        }
                    }


                    if (ImageViewModel.SelectDrawingVisuals != null)
                    {
                        foreach (var item in ImageViewModel.SelectDrawingVisuals)
                        {
                            if (item is IRectangle rectangle)
                            {
                                var OldRect = rectangle.Rect;
                                rectangle.Rect = new Rect(OldRect.X + point.X - LastMouseMove.X, OldRect.Y + point.Y - LastMouseMove.Y, OldRect.Width, OldRect.Height);
                            }
                            else if (item is ICircle Circl)
                            {
                                Circl.Center += point - LastMouseMove;
                            }
                        }
                    }


                }

                if (IsRightButtonDown)
                {
                    if (ImageViewModel.SelectDrawingVisual is ICircle circle)
                    {
                        double Radius = Math.Sqrt((Math.Pow(point.X - MouseDownP.X, 2) + Math.Pow(point.Y - MouseDownP.Y, 2)));
                        circle.Radius = Radius;
                    }

                    if (ImageViewModel.SelectDrawingVisual is IRectangle rectangle)
                    {
                        var OldRect = rectangle.Rect;
                        double x = OldRect.X;
                        double y = OldRect.Y;
                        double width = OldRect.Width + point.X - LastMouseMove.X;
                        double height = OldRect.Height + point.Y - LastMouseMove.Y;
                        if (width >= 1 && height >=1)
                        {
                            rectangle.Rect = new Rect(x, y, width, height);
                        }
                    }
                }
                LastMouseMove = point;

            }
        }

        private double DefalutWidth { get; set; } = 30;
        private double DefalutHeight { get; set; } = 30;
        private double DefalutRadius { get; set; } = 30;


        private void ImageShow_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is DrawCanvas drawCanvas && !Keyboard.Modifiers.HasFlag(Zoombox1.ActivateOn))
            {
                if (IsMouseDown)
                {
                    IsMouseDown = false;
                    var MouseUpP = e.GetPosition(drawCanvas);

                    if (drawCanvas.GetVisual(MouseUpP) is not DrawingVisual dv ||  ImageViewModel.SelectDrawingVisuals == null || !ImageViewModel.SelectDrawingVisuals.Contains(dv))
                        SelectDrawingVisualsClear();

                    if (drawCanvas.ContainsVisual(SelectRect))
                    {
                        if (ImageViewModel.EraseVisual)
                        {
                            drawCanvas.RemoveVisual(drawCanvas.GetVisual(MouseDownP));
                            drawCanvas.RemoveVisual(drawCanvas.GetVisual(MouseUpP));
                            foreach (var item in drawCanvas.GetVisuals(new RectangleGeometry(new Rect(MouseDownP, MouseUpP))))
                            {
                                drawCanvas.RemoveVisual(item);
                            }
                        }
                        else
                        {
                            ImageViewModel.SelectDrawingVisuals = drawCanvas.GetVisuals(new RectangleGeometry(new Rect(MouseDownP, MouseUpP)));
                            foreach (var item in ImageViewModel.SelectDrawingVisuals)
                            {
                                if (item is IDrawingVisual drawingVisual)
                                {
                                    drawingVisual.Pen.Brush = Brushes.Yellow;
                                    drawingVisual.Render();
                                }
                            }

                            if (ImageViewModel.SelectDrawingVisuals.Count == 0)
                                ImageViewModel.SelectDrawingVisuals = null;
                        }

                        drawCanvas.RemoveVisual(SelectRect,false);
                    }


                    if (ImageViewModel.DrawPolygon)
                    {
                        if (DrawingVisualPolygonCache != null)
                        {
                            DrawingVisualPolygonCache.Points.Add(MouseUpP);
                            DrawingVisualPolygonCache.MovePoints = null;
                            DrawingVisualPolygonCache.Render();
                        }

                    }
                    else if (ImageViewModel.DrawCircle)
                    {
                        if (DrawCircleCache.Attribute.Radius == DefalutRadius)
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

                        DefalutRadius = DrawCircleCache.Radius;

                    }
                    else if (ImageViewModel.DrawRect)
                    {
                        if (DrawingRectangleCache.Attribute.Rect.Width == DefalutWidth && DrawingRectangleCache.Attribute.Rect.Height == DefalutHeight)
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


                        DefalutWidth = DrawingRectangleCache.Attribute.Rect.Width;
                        DefalutHeight = DrawingRectangleCache.Attribute.Rect.Height;
                    }

                    drawCanvas.ReleaseMouseCapture();

                    if (ImageViewModel.SelectDrawingVisual is DVCircle circle)
                    {
                        circle.IsDrawing = false;
                        circle.Render();
                    }

                    if (ImageViewModel.SelectDrawingVisual != null)
                    {
                        if (ImageViewModel.SelectDrawingVisual is IRectangle rectangle)
                        {
                            var l = MouseUpP - MouseDownP;

                            Action undoaction = new Action(() =>
                            {
                                var OldRect = rectangle.Rect;
                                rectangle.Rect = new Rect(OldRect.X - l.X, OldRect.Y - l.Y, OldRect.Width, OldRect.Height);
                            });
                            Action redoaction = new Action(() =>
                            {
                                var OldRect = rectangle.Rect;
                                rectangle.Rect = new Rect(OldRect.X + l.X, OldRect.Y + l.Y, OldRect.Width, OldRect.Height);
                            });
                            ImageShow.AddActionCommand(new ActionCommand(undoaction, redoaction) { Header = "移动矩形" });
                        }
                        else if (ImageViewModel.SelectDrawingVisual is ICircle Circl)
                        {
                            var l = MouseUpP - MouseDownP;
                            Action undoaction = new Action(() =>
                            {
                                Circl.Center -= l;
                            });
                            Action redoaction = new Action(() =>
                            {
                                Circl.Center += l;
                            });
                            ImageShow.AddActionCommand(new ActionCommand(undoaction, redoaction) { Header ="移动圆"});
                        }
                    }
                }
                if (IsRightButtonDown)
                {
                    IsRightButtonDown = false;
                }
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
                if (ImageViewModel.EraseVisual)
                {
                    ToggleButtonDrag.IsChecked = true;
                    Zoombox1.ActivateOn = toggleButton.IsChecked == true ? ModifierKeys.Control : ModifierKeys.None;
                }
            }
        }

        public void Clear()
        {
            Config.Properties.Clear();
            FunctionImage = null;
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

        private List<SelectionChangedEventHandler> _handlers = new List<SelectionChangedEventHandler>();
        public void AddSelectionChangedHandler(SelectionChangedEventHandler handler)
        {
            ComboBoxLayers.SelectionChanged += handler;
            _handlers.Add(handler);
        }

        public void ClearSelectionChangedHandlers()
        {
            foreach (var handler in _handlers)
            {
                ComboBoxLayers.SelectionChanged -= handler;
            }
            _handlers.Clear();
        }

        public IImageOpen? IImageOpen { get; set; }

        public async void OpenImage(string? filePath)
        {
            //如果文件已经打开，不会重复打开
            if (filePath ==null || filePath.Equals(Config.FilePath, StringComparison.Ordinal)) return;
            Button1931.Visibility = Visibility.Collapsed;
            Config.AddProperties("FilePath", filePath);
            ClearSelectionChangedHandlers();
            Config.FilePath = filePath;
            if (filePath != null && File.Exists(filePath))
            {
                long fileSize = new FileInfo(filePath).Length;
                Config.AddProperties("FileSize", fileSize);

                bool isLargeFile = fileSize > 1024 * 1024 * 100;//例如，文件大于1MB时认为是大文件

                string ext = Path.GetExtension(filePath).ToLower(CultureInfo.CurrentCulture);
                IImageOpen = ImageComponentManager.GetInstance().IImageViewOpens.FirstOrDefault(a => a.Extension.Any(b => ext.Contains(b)));
                if (IImageOpen != null)
                {
                    Config.AddProperties("ImageViewOpen", IImageOpen);
                    IImageOpen.OpenImage(this, filePath);
                    return;
                }

                try
                {
                    ComboBoxLayers.SelectedIndex = 0;
                    ComboBoxLayers.ItemsSource = ComboBoxLayerItems;
                    AddSelectionChangedHandler(ComboBoxLayers_SelectionChanged);

                    if (Config.IsShowLoadImage && isLargeFile)
                    {
                        WaitControl.Visibility = Visibility.Visible;
                        await Task.Run(() =>
                        {
                            byte[] imageData = File.ReadAllBytes(filePath);
                            BitmapImage bitmapImage = ImageUtils.CreateBitmapImage(imageData);
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                SetImageSource(bitmapImage.ToWriteableBitmap());
                                UpdateZoomAndScale();
                                WaitControl.Visibility = Visibility.Collapsed;
                            });
                        });

                    }
                    else
                    {
                        BitmapImage bitmapImage = new BitmapImage(new Uri(filePath));
                        SetImageSource(bitmapImage.ToWriteableBitmap());
                        UpdateZoomAndScale();
                    };

                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }

            }


        }

        public HImage? HImageCache { get; set; }

        public void SetImageSource(ImageSource imageSource)
        {
            ToolBarLayers.Visibility = Visibility.Visible;
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
                        Config.AddProperties("Cols", hImage.cols);
                        Config.AddProperties("Rows", hImage.rows);
                        Config.AddProperties("Channel", hImage.channels);
                        Config.AddProperties("Depth", hImage.depth);
                        Config.AddProperties("Stride", hImage.stride);
                        Config.Channel = hImage.channels;
                        Config.Ochannel = Config.Channel;
                    }
                })));
            }

            ViewBitmapSource = imageSource;
            ImageShow.Source = ViewBitmapSource;

            ImageShow.ImageInitialize();
            ImageViewModel.ToolBarScaleRuler.IsShow = true;
        }


        public ImageSource FunctionImage { get; set; }
        public ImageSource ViewBitmapSource { get; set; }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            RenderPseudo();
        }

        private void PseudoSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<HandyControl.Data.DoubleRange> e)
        {
            DebounceTimer.AddOrResetTimer("PseudoSlider", 50, (e) =>
            {
                RenderPseudo();
            }, e.NewValue);
        }
        public void RenderPseudo()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Pseudo.IsChecked == false)
                {
                    ImageShow.Source = ViewBitmapSource;
                    FunctionImage = null;
                    return;
                }

                if (HImageCache != null)
                {
                    // 首先获取滑动条的值，这需要在UI线程中执行

                    uint min = (uint)PseudoSlider.ValueStart;
                    uint max = (uint)PseudoSlider.ValueEnd;
                    int channel = ComboBoxLayers.SelectedIndex - 1;

                    log.Info($"ImagePath，正在执行PseudoColor,min:{min},max:{max}");
                    Task.Run(() =>
                    {
                        int ret = OpenCVMediaHelper.M_PseudoColor((HImage)HImageCache, out HImage hImageProcessed, min, max, Config.ColormapTypes, channel);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (ret == 0)
                            {
                                if (!HImageExtension.UpdateWriteableBitmap(FunctionImage, hImageProcessed))
                                {
                                    var image = hImageProcessed.ToWriteableBitmap();
                                    OpenCVMediaHelper.M_FreeHImageData(hImageProcessed.pData);
                                    hImageProcessed.pData = IntPtr.Zero;
                                    FunctionImage = image;
                                }
                                if (Pseudo.IsChecked == true)
                                {
                                    ImageShow.Source = FunctionImage;
                                }
                            }
                        });
                    });
                };
            });
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
            Open();
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
                menuPop1.IsOpen = false;
            }
        }


        private void HistogramButton_Click(object sender, RoutedEventArgs e)
        {
            if (ImageShow.Source is not BitmapSource bitmapSource)  return;

            var (redHistogram, greenHistogram, blueHistogram) = ImageUtils.RenderHistogram(bitmapSource);
            if (bitmapSource.Format.Masks.Count ==1)
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

        public List<string> ComboBoxLayerItems { get; set; } = new List<string>() { "Src" ,"R","G","B" };

        public void ComboBoxLayers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBoxLayers.SelectedIndex < 0) return;

            if (ComboBoxLayerItems[ComboBoxLayers.SelectedIndex] == "Src")
                ExtractChannel(-1);
            if (ComboBoxLayerItems[ComboBoxLayers.SelectedIndex] == "R")
                ExtractChannel(2);
            if (ComboBoxLayerItems[ComboBoxLayers.SelectedIndex] == "G")
                ExtractChannel(1);
            if (ComboBoxLayerItems[ComboBoxLayers.SelectedIndex] == "B")
                ExtractChannel(0);
        }

        public void ExtractChannel(int channel)
        {
            if (ViewBitmapSource == null) return;

            if (channel == -1)
            {
                ImageShow.Source = ViewBitmapSource;
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
                        if (!HImageExtension.UpdateWriteableBitmap(FunctionImage, hImageProcessed))
                        {
                            var image = hImageProcessed.ToWriteableBitmap();

                            OpenCVMediaHelper.M_FreeHImageData(hImageProcessed.pData);
                            hImageProcessed.pData = IntPtr.Zero;
                            FunctionImage = image;
                        }
                        ImageShow.Source = FunctionImage;
                        Config.Channel = 1;
                    }
                });
            });

        }
        bool IsUpdateZoomAndScale = true;

        public void UpdateZoomAndScale()
        {
            if (IsUpdateZoomAndScale)
            {
                Task.Run(() => {
                    Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        Zoombox1.ZoomUniform();
                        ImageViewModel.ToolBarScaleRuler.Render();
                    });
                });
                IsUpdateZoomAndScale = false;
            }

        }

        private void CM_AutoLevelsAdjust(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton toggleButton) return;
            if (toggleButton.IsChecked == false)
            {
                ImageShow.Source = ViewBitmapSource;
                FunctionImage = null;
                return;
            }
            if (HImageCache != null)
            {
                int ret = OpenCVMediaHelper.M_AutoLevelsAdjust((HImage)HImageCache, out HImage hImageProcessed);
                if (ret == 0)
                {
                    if (!HImageExtension.UpdateWriteableBitmap(FunctionImage, hImageProcessed))
                    {
                        var image = hImageProcessed.ToWriteableBitmap();

                        OpenCVMediaHelper.M_FreeHImageData(hImageProcessed.pData);
                        hImageProcessed.pData = IntPtr.Zero;
                        FunctionImage = image;
                    }
                    ImageShow.Source = FunctionImage;
                }
            };
        }

        public void AdjustWhiteBalance()
        {
            if (HImageCache != null)
            {
                int ret = OpenCVMediaHelper.M_GetWhiteBalance((HImage)HImageCache, out HImage hImageProcessed, Config.RedBalance, Config.GreenBalance, Config.BlueBalance);
                if (ret == 0)
                {
                    Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        if (!HImageExtension.UpdateWriteableBitmap(FunctionImage, hImageProcessed))
                        {
                            var image = hImageProcessed.ToWriteableBitmap();

                            OpenCVMediaHelper.M_FreeHImageData(hImageProcessed.pData);
                            hImageProcessed.pData = IntPtr.Zero;
                            FunctionImage = image;
                        }
                        ImageShow.Source = FunctionImage;
                    });
                }
            };
        }

        private void CM_AutomaticColorAdjustment(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton toggleButton) return;
            if (toggleButton.IsChecked == false)
            {
                ImageShow.Source = ViewBitmapSource;
                FunctionImage = null;
                return;
            }
            if (HImageCache != null)
            {
                int ret = OpenCVMediaHelper.M_AutomaticColorAdjustment((HImage)HImageCache, out HImage hImageProcessed);
                if (ret == 0)
                {
                    if (!HImageExtension.UpdateWriteableBitmap(FunctionImage, hImageProcessed))
                    {
                        var image = hImageProcessed.ToWriteableBitmap();

                        OpenCVMediaHelper.M_FreeHImageData(hImageProcessed.pData);
                        hImageProcessed.pData = IntPtr.Zero;
                        FunctionImage = image;
                    }
                    ImageShow.Source = FunctionImage;
                }
            };
        }

        private void CM_AutomaticToneAdjustment(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton toggleButton) return;
            if (toggleButton.IsChecked == false)
            {
                ImageShow.Source = ViewBitmapSource;
                FunctionImage = null;
                return;
            }
            if (HImageCache == null) return;

            int ret = OpenCVHelper.CM_AutomaticToneAdjustment((HImage)HImageCache, out HImage hImageProcessed);
            if (ret == 0)
            {
                if (!HImageExtension.UpdateWriteableBitmap(FunctionImage, hImageProcessed))
                {
                    var image = hImageProcessed.ToWriteableBitmap();

                    OpenCVMediaHelper.M_FreeHImageData(hImageProcessed.pData);
                    hImageProcessed.pData = IntPtr.Zero;
                    FunctionImage = image;
                }
                ImageShow.Source = FunctionImage;
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

        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    ImageViewModel.ClearImageEventHandler -= Clear;
                    ImageViewModel.Dispose();
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

        private void PreviewSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            DebounceTimer.AddOrResetTimer("ApplyGammaCorrection", 50, a=> ApplyGammaCorrection(a), GammaSlider.Value);
        }

        public void ApplyGammaCorrection(double Gamma)
        {
            if (HImageCache == null) return;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            log.Info($"ImagePath，正在执行ApplyGammaCorrection,Gamma{Gamma}");
            int ret = OpenCVMediaHelper.M_ApplyGammaCorrection((HImage)HImageCache, out HImage hImageProcessed, Gamma);
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (ret == 0)
                {
                    if (!HImageExtension.UpdateWriteableBitmap(FunctionImage, hImageProcessed))
                    {
                        var image = hImageProcessed.ToWriteableBitmap();
                        OpenCVMediaHelper.M_FreeHImageData(hImageProcessed.pData);
                        hImageProcessed.pData = IntPtr.Zero;
                        FunctionImage = image;
                    }
                    ImageShow.Source = FunctionImage;
                    stopwatch.Stop();
                    log.Info($"ApplyGammaCorrection {stopwatch.Elapsed}");
                }
            });
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (FunctionImage is WriteableBitmap writeableBitmap)
            {
                ViewBitmapSource = writeableBitmap;
                ImageShow.Source = ViewBitmapSource; ;
                HImageCache = writeableBitmap.ToHImage();
                Config.Channel = HImageCache.Value.channels;
                FunctionImage = null;
                GammaSlider.Value = 1;
                Config.RedBalance = 1;
                Config.GreenBalance = 1;
                Config.BlueBalance = 1;
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            GammaSlider.Value = 1;
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            Config.RedBalance = 1;
            Config.GreenBalance = 1;
            Config.BlueBalance = 1;
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            OpenImage(Config.FilePath);
        }

        private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            DebounceTimer.AddOrResetTimer("AdjustBrightnessContrast", 50, AdjustBrightnessContrast, ContrastSlider.Value, BrightnessSlider.Value);
        }
        private void ContrastSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            DebounceTimer.AddOrResetTimer("AdjustBrightnessContrast", 50, AdjustBrightnessContrast, ContrastSlider.Value, BrightnessSlider.Value);
        }
        public void AdjustBrightnessContrast(double Contrast ,double Brightness)
        {
            if (HImageCache == null) return;
            //实现类似于PS的效果
            Brightness = Brightness * 4 / 5;
            Contrast = Contrast / 300 + 1;
            Brightness = HImageCache.Value.depth == 8 ? Brightness : Brightness * 255;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            log.Info($"ImagePath，正在执行AdjustBrightnessContrast,Brightness{Brightness},Contrast{Contrast}");
            int ret = OpenCVMediaHelper.M_AdjustBrightnessContrast((HImage)HImageCache, out HImage hImageProcessed, Contrast, Brightness);
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (ret == 0)
                {
                    if (!HImageExtension.UpdateWriteableBitmap(FunctionImage, hImageProcessed))
                    {
                        var image = hImageProcessed.ToWriteableBitmap();
                        OpenCVMediaHelper.M_FreeHImageData(hImageProcessed.pData);
                        hImageProcessed.pData = IntPtr.Zero;
                        FunctionImage = image;
                    }
                    ImageShow.Source = FunctionImage;
                    stopwatch.Stop();
                    log.Info($"AdjustBrightnessContrast {stopwatch.Elapsed}");
                }
            });
        }

        public void InvertImag()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (HImageCache == null) return;
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                log.Info($"InvertImag");
                Task.Run(() =>
                {
                    int ret = OpenCVMediaHelper.M_InvertImage((HImage)HImageCache, out HImage hImageProcessed);
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        if (ret == 0)
                        {
                            if (!HImageExtension.UpdateWriteableBitmap(FunctionImage, hImageProcessed))
                            {
                                var image = hImageProcessed.ToWriteableBitmap();
                                OpenCVMediaHelper.M_FreeHImageData(hImageProcessed.pData);
                                hImageProcessed.pData = IntPtr.Zero;
                                FunctionImage = image;
                            }
                            ImageShow.Source = FunctionImage;
                            stopwatch.Stop();
                            log.Info($"InvertImag {stopwatch.Elapsed}");
                        }
                    });
                });
            });
        }

        private void Button_Click_InvertImage(object sender, RoutedEventArgs e)
        {
            InvertImag();
        }


        void ThresholdImg()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (HImageCache == null) return;
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                double thresh = thresholdSlider.Value;
                double maxval = 65535;
                int type = 0;
                log.Info($"InvertImag");
                Task.Run(() =>
                {
                    int ret = OpenCVMediaHelper.M_Threshold((HImage)HImageCache, out HImage hImageProcessed, thresh, maxval,type);
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        if (ret == 0)
                        {
                            if (!HImageExtension.UpdateWriteableBitmap(FunctionImage, hImageProcessed))
                            {
                                var image = hImageProcessed.ToWriteableBitmap();
                                OpenCVMediaHelper.M_FreeHImageData(hImageProcessed.pData);
                                hImageProcessed.pData = IntPtr.Zero;
                                FunctionImage = image;
                            }
                            ImageShow.Source = FunctionImage;
                            stopwatch.Stop();
                            log.Info($"InvertImag {stopwatch.Elapsed}");
                        }
                    });
                });
            });
        }

        private void Threshold_Click(object sender, RoutedEventArgs e)
        {
            ThresholdImg();
        }

        private void thresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            DebounceTimer.AddOrResetTimer("AdjustBrightnessContrast", 50, a => ThresholdImg(), e.NewValue);
        }
    }
}
