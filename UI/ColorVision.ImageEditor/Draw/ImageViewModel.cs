#pragma warning disable CS8625,CS8602,CS8607,CS0103,CS0067
using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Ruler;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.UI;
using ColorVision.UI.Menus;
using Gu.Wpf.Geometry;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor
{

    public interface IImageContentMenuProvider
    {
        public List<MenuItemMetadata> GetContextMenuItems(ImageViewConfig config);
    }


    public class ImageViewModel : ViewModelBase,IDisposable
    {
        public RelayCommand ZoomUniformToFill { get; set; }
        public RelayCommand ZoomUniformCommand { get; set; }
        public RelayCommand ZoomInCommand { get; set; }
        public RelayCommand ZoomOutCommand { get; set; }
        public RelayCommand ZoomNoneCommand { get; set; }

        public RelayCommand FullCommand { get; set; }
        public RelayCommand RotateLeftCommand { get; set; }
        public RelayCommand RotateRightCommand { get; set; }
        public RelayCommand FlipHorizontalCommand { get; set; }
        public RelayCommand FlipVerticalCommand { get; set; }

        public RelayCommand SaveAsImageCommand { get; set; }

        public RelayCommand ClearImageCommand { get; set; }

        public event EventHandler ClearImageEventHandler;

        public event EventHandler<string> OpenedImage;
        public event EventHandler<string> OpeningImage;


        public RelayCommand PrintImageCommand { get; set; }

        public RelayCommand PropertyCommand { get; set; }

        public RelayCommand OpenImageCommand { get; set; }

        public ZoomboxSub ZoomboxSub { get; set; }

        private DrawCanvas Image { get; set; }

        public BezierCurveManager BezierCurveManager { get; set; }

        public CircleManager CircleManager { get; set; }

        public RectangleManager RectangleManager { get; set; }

        public EraseManager EraseManager { get; set; }

        public PolygonManager PolygonManager { get; set; }


        public MouseMagnifier MouseMagnifier { get; set; }

        public LineManager LineManager { get; set; }

        public Crosshair Crosshair { get; set; }
        public Gridline Gridline { get; set; }

        private ToolBarMeasure ToolBarMeasure { get; set; }

        private FrameworkElement Parent { get; set; }

        public ToolBarScaleRuler ToolBarScaleRuler { get; set; }

        public ToolReferenceLine ToolConcentricCircle { get; set; }

        public ObservableCollection<IDrawingVisual> DrawingVisualLists { get; set; } = new ObservableCollection<IDrawingVisual>();
        public SelectEditorVisual SelectEditorVisual { get; set; }

        public ImageViewConfig Config { get; set; }

        public ContextMenu ContextMenu { get; set; }
        public IImageOpen? IImageOpen { get; set; }

        public ImageViewModel(FrameworkElement Parent,ZoomboxSub zoombox, DrawCanvas drawCanvas,ImageViewConfig config = null )
        {
            Config = config ?? new ImageViewConfig();
            SelectEditorVisual = new SelectEditorVisual(this, drawCanvas, zoombox);
            drawCanvas.CommandBindings.Add(new CommandBinding(ApplicationCommands.Print, (s, e) => Print(), (s, e) => { e.CanExecute = Image != null && Image.Source != null; }));
            drawCanvas.CommandBindings.Add(new CommandBinding(ApplicationCommands.SaveAs, (s, e) => SaveAs(), (s, e) => { e.CanExecute = Image != null && Image.Source != null; }));
            drawCanvas.CommandBindings.Add(new CommandBinding(ApplicationCommands.Open, (s, e) => OpenImage(), (s, e) => { e.CanExecute = true; }));
            drawCanvas.CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (s, e) => ClearImage(), (s, e) => { e.CanExecute = Image.Source != null; }));
            OpenImageCommand = new RelayCommand(a => OpenImage());

            this.Parent = Parent;
            drawCanvas.PreviewMouseDown += (s, e) =>
            {
                Keyboard.ClearFocus(); // 清除当前焦点
                drawCanvas.Focus();
            };
            drawCanvas.PreviewKeyDown += (s, e) =>
            {
                Keyboard.ClearFocus(); // 清除当前焦点
                drawCanvas.Focus();
            };

            ZoomboxSub = zoombox ?? throw new ArgumentNullException(nameof(zoombox));
            Image = drawCanvas ?? throw new ArgumentNullException(nameof(drawCanvas));

            MouseMagnifier = new MouseMagnifier(zoombox, drawCanvas);
            Crosshair = new Crosshair(zoombox, drawCanvas);
            Gridline = new Gridline(zoombox, drawCanvas);
            ToolBarMeasure = new ToolBarMeasure(Parent, zoombox, drawCanvas);
            ToolBarScaleRuler = new ToolBarScaleRuler(Parent, zoombox, drawCanvas);
            ToolConcentricCircle = new ToolReferenceLine(this,zoombox, drawCanvas);

            PolygonManager = new PolygonManager(this, zoombox, drawCanvas);
            BezierCurveManager = new BezierCurveManager(this, zoombox, drawCanvas);
            LineManager = new LineManager(this, zoombox, drawCanvas);

            CircleManager = new CircleManager(this, zoombox, drawCanvas);
            RectangleManager = new RectangleManager(this, zoombox, drawCanvas);
            EraseManager = new EraseManager(this, zoombox, drawCanvas);

            ZoomUniformToFill = new RelayCommand(a => ZoomboxSub.ZoomUniformToFill(), a => Image != null && Image.Source != null);
            ZoomUniformCommand = new RelayCommand(a => ZoomboxSub.ZoomUniform(),a => Image != null && Image.Source != null);
            ZoomInCommand = new RelayCommand(a => ZoomboxSub.Zoom(1.25), a => Image != null && Image.Source != null);
            ZoomOutCommand = new RelayCommand(a => ZoomboxSub.Zoom(0.8), a => Image != null &&  Image.Source != null);
            ZoomNoneCommand = new RelayCommand(a => ZoomboxSub.ZoomNone(), a => Image != null && Image.Source != null);

            FlipHorizontalCommand = new RelayCommand(a => FlipHorizontal(), a => Image != null && Image.Source != null);
            FlipVerticalCommand = new RelayCommand(a =>FlipVertical(), a => Image != null && Image.Source != null);
            drawCanvas.PreviewKeyDown += PreviewKeyDown;
            zoombox.Cursor = Cursors.Arrow;
            SaveAsImageCommand = new RelayCommand(a => SaveAs(),a=> Image!=null && Image.Source!=null);

            PrintImageCommand = new RelayCommand(a => Print(), a => Image != null && Image.Source != null);

            ClearImageCommand = new RelayCommand(a => ClearImage(),a => Image != null && Image.Source != null);
            FullCommand = new RelayCommand(a => MaxImage());

            RotateLeftCommand = new RelayCommand(a => RotateLeft());
            RotateRightCommand = new RelayCommand(a => RotateRight());

            ContextMenu = new ContextMenu();
            Image.ContextMenuOpening += ContextMenu_ContextMenuOpening;
            Image.ContextMenu = ContextMenu;
            ZoomboxSub.ContextMenu = ContextMenu;
        }

        private void ContextMenu_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            ContextMenu.Items.Clear();
            if (_ImageEditMode)
            {
                Point MouseDownP = Mouse.GetPosition(Image);

                var MouseVisual = Image.GetVisual<Visual>(MouseDownP);

                if (MouseVisual is SelectEditorVisual selectEditorVisual && selectEditorVisual.GetVisual(MouseDownP) is ISelectVisual selectVisual)
                {
                    if (selectVisual is IDrawingVisual drawingVisual)
                    {
                        MenuItem menuItem = new() { Header = "隐藏(_H)" };
                        menuItem.Click += (s, e) =>
                        {
                            drawingVisual.BaseAttribute.IsShow = false;
                            selectEditorVisual.ClearRender();
                        };
                        ContextMenu.Items.Add(menuItem);
                    }
                    if (selectVisual is Visual visual)
                    {
                        MenuItem menuIte2 = new() { Header = "删除" };
                        menuIte2.Click += (s, e) =>
                        {
                            Image.RemoveVisualCommand(visual);
                            selectEditorVisual.ClearRender();
                        };
                        ContextMenu.Items.Add(menuIte2);

                        MenuItem menuIte3 = new() { Header = "Top" };
                        menuIte3.Click += (s, e) =>
                        {
                            Image.TopVisual(visual);
                        };
                        ContextMenu.Items.Add(menuIte3);
                    }


                }
                else if (MouseVisual is IDrawingVisual drawingVisual)
                {
                    MenuItem menuItem = new() { Header = "隐藏(_H)" };
                    menuItem.Click += (s, e) =>
                    {
                        drawingVisual.BaseAttribute.IsShow = false;
                    };
                    MenuItem menuIte2 = new() { Header = "删除" };
                    menuIte2.Click += (s, e) =>
                    {
                        Image.RemoveVisualCommand(MouseVisual);
                    };
                    ContextMenu.Items.Add(menuItem);
                    ContextMenu.Items.Add(menuIte2);

                    MenuItem menuIte3 = new() { Header = "Top" };
                    menuIte3.Click += (s, e) =>
                    {
                        Image.TopVisual(MouseVisual);
                    };
                    ContextMenu.Items.Add(menuIte3);
                }
                else
                {
                    Opened();
                }
            }
            else
            {
                Opened();
            }
        }


        public void Opened()
        {
            List<MenuItemMetadata> MenuItemMetadatas = new List<MenuItemMetadata>();
            if (IImageOpen != null)
                MenuItemMetadatas.AddRange(IImageOpen.GetContextMenuItems(Config));

            foreach (var item in AssemblyService.Instance.LoadImplementations<IImageContentMenuProvider>())
            {
                MenuItemMetadatas.AddRange(item.GetContextMenuItems(Config));
            }


            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "OpenImage", Order = 10, Header = ColorVision.ImageEditor.Properties.Resources.Open, Command = OpenImageCommand, Icon = MenuItemIcon.TryFindResource("DIOpen") });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "ClearImage", Order = 11, Header = ColorVision.ImageEditor.Properties.Resources.Clear, Command = ClearImageCommand, Icon = MenuItemIcon.TryFindResource("DIDelete") });

            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "Zoom", Order = 100, Header = Properties.Resources.Zoom, Icon = MenuItemIcon.TryFindResource("DIZoom") });
            MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "Zoom", GuidId = "ZoomIn", Order = 1, Header = Properties.Resources.ZoomIn, Command = ZoomInCommand });
            MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "Zoom", GuidId = "ZoomOut", Order = 2, Header = Properties.Resources.ZoomOut, Command = ZoomOutCommand });
            MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "Zoom", GuidId = "ZoomNone", Order = 3, Header = ColorVision.ImageEditor.Properties.Resources.ZoomNone, Command = ZoomNoneCommand });
            MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "Zoom", GuidId = "ZoomUniform", Order = 4, Header = ColorVision.ImageEditor.Properties.Resources.ZoomUniform, Command = ZoomUniformCommand });

            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "Rotate", Order = 101, Header = ColorVision.ImageEditor.Properties.Resources.Rotate, Icon = MenuItemIcon.TryFindResource("DIRotate") });
            MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "Rotate", GuidId = "RotateLeft", Order = 1, Header = ColorVision.ImageEditor.Properties.Resources.RotateLeft, Command = RotateLeftCommand, Icon = MenuItemIcon.TryFindResource("DIRotateLeft") });
            MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "Rotate", GuidId = "RotateRight", Order = 2, Header = ColorVision.ImageEditor.Properties.Resources.RotateRight, Command = RotateRightCommand, Icon = MenuItemIcon.TryFindResource("DIRotateRight") });
            MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "Rotate", GuidId = "FlipHorizontal", Order = 3, Header = ColorVision.ImageEditor.Properties.Resources.FlipHorizontal, Command = FlipHorizontalCommand, Icon = MenuItemIcon.TryFindResource("DIFlipHorizontal") });
            MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "Rotate", GuidId = "FlipVertical", Order = 4, Header = ColorVision.ImageEditor.Properties.Resources.FlipVertical, Command = FlipVerticalCommand, Icon = MenuItemIcon.TryFindResource("DIFlipVertical") });

            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "Full", Order = 200, Header = ColorVision.ImageEditor.Properties.Resources.FullScreen, Command = FullCommand });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "SaveAsImage", Order = 300, Header = ColorVision.ImageEditor.Properties.Resources.SaveAsImage, Command = SaveAsImageCommand });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "Print", Order = 300, Header = ColorVision.ImageEditor.Properties.Resources.Print, Command = PrintImageCommand, Icon = MenuItemIcon.TryFindResource("DIPrint"), InputGestureText = "Ctrl+P" });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "Property", Order = 9999, Command = PropertyCommand, Header = ColorVision.ImageEditor.Properties.Resources.Property, Icon = MenuItemIcon.TryFindResource("DIProperty"), InputGestureText = "Tab" });

            var iMenuItems = MenuItemMetadatas.OrderBy(item => item.Order).ToList();

            void CreateMenu(MenuItem parentMenuItem, string OwnerGuid)
            {
                var iMenuItems1 = iMenuItems.FindAll(a => a.OwnerGuid == OwnerGuid).OrderBy(a => a.Order).ToList();
                for (int i = 0; i < iMenuItems1.Count; i++)
                {
                    var iMenuItem = iMenuItems1[i];
                    string GuidId = iMenuItem.GuidId ?? Guid.NewGuid().ToString();
                    MenuItem menuItem;

                    menuItem = new MenuItem
                    {
                        Header = iMenuItem.Header,
                        Icon = iMenuItem.Icon,
                        InputGestureText = iMenuItem.InputGestureText,
                        Command = iMenuItem.Command,
                        Tag = iMenuItem,
                        Visibility = iMenuItem.Visibility,
                    };

                    CreateMenu(menuItem, GuidId);
                    if (i > 0 && iMenuItem.Order - iMenuItems1[i - 1].Order > 4 && iMenuItem.Visibility == Visibility.Visible)
                    {
                        parentMenuItem.Items.Add(new Separator());
                    }
                    parentMenuItem.Items.Add(menuItem);
                }
                foreach (var item in iMenuItems1)
                {
                    iMenuItems.Remove(item);
                }
            }

            var iMenuItemMetas = MenuItemMetadatas.Where(item => item.OwnerGuid == MenuItemConstants.Menu && item.Visibility == Visibility.Visible).OrderBy(item => item.Order).ToList();

            for (int i = 0; i < iMenuItemMetas.Count; i++)
            {
                MenuItemMetadata menuItemMeta = iMenuItemMetas[i];
                MenuItem menuItem = new MenuItem()
                {
                    Header = menuItemMeta.Header,
                    Command = menuItemMeta.Command,
                    Icon = menuItemMeta.Icon,
                    InputGestureText = menuItemMeta.InputGestureText,
                };
                if (menuItemMeta.GuidId != null)
                    CreateMenu(menuItem, menuItemMeta.GuidId);
                if (i > 0 && menuItemMeta.Order - iMenuItemMetas[i - 1].Order > 4)
                    ContextMenu.Items.Add(new Separator());

                ContextMenu.Items.Add(menuItem);
            }

            MenuItem menuItemBitmapScalingMode = new() { Header = ColorVision.ImageEditor.Properties.Resources.BitmapScalingMode };
            void UpdateBitmapScalingMode()
            {
                var ime = RenderOptions.GetBitmapScalingMode(Image);
                menuItemBitmapScalingMode.Items.Clear();
                foreach (var item in Enum.GetValues(typeof(BitmapScalingMode)).Cast<BitmapScalingMode>().GroupBy(mode => (int)mode).Select(group => group.First()))
                {
                    MenuItem menuItem1 = new() { Header = item.ToString() };
                    if (ime != item)
                    {
                        menuItem1.Click += (s, e) =>
                        {
                            RenderOptions.SetBitmapScalingMode(Image, item);
                        };
                    }
                    menuItem1.IsChecked = ime == item;
                    menuItemBitmapScalingMode.Items.Add(menuItem1);
                }
            }
            menuItemBitmapScalingMode.SubmenuOpened += (s, e) => UpdateBitmapScalingMode();
            UpdateBitmapScalingMode();
            ContextMenu.Items.Insert(4, menuItemBitmapScalingMode);

        }

        public void OpenImage()
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                OpeningImage?.Invoke(this, openFileDialog.FileName);
            }
        }

        public void Print()
        {
            PrintDialog printDialog = new();
            if (printDialog.ShowDialog() == true)
            {
                // 创建一个可打印的区域
                Size pageSize = new(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight);
                Image.Measure(pageSize);
                Image.Arrange(new Rect(5, 5, pageSize.Width, pageSize.Height));

                // 开始打印
                printDialog.PrintVisual(Image, "Printing");
            }

        }


        public void FlipHorizontal()
        {
            if (Image.RenderTransform is TransformGroup transformGroup)
            {
                var scaleTransform = transformGroup.Children.OfType<ScaleTransform>().FirstOrDefault();
                if (scaleTransform != null)
                {
                    scaleTransform.ScaleX *= -1;
                }
                else
                {
                    scaleTransform = new ScaleTransform { ScaleX = -1 };
                    transformGroup.Children.Add(scaleTransform);
                }
            }
            else
            {
                transformGroup = new TransformGroup();
                transformGroup.Children.Add(new ScaleTransform { ScaleX = -1 });
                Image.RenderTransform = transformGroup;
                Image.RenderTransformOrigin = new Point(0.5, 0.5);
            }
        }

        public void FlipVertical()
        {
            if (Image.RenderTransform is TransformGroup transformGroup)
            {
                var scaleTransform = transformGroup.Children.OfType<ScaleTransform>().FirstOrDefault();
                if (scaleTransform != null)
                {
                    scaleTransform.ScaleY *= -1;
                }
                else
                {
                    scaleTransform = new ScaleTransform { ScaleY = -1 };
                    transformGroup.Children.Add(scaleTransform);
                }
            }
            else
            {
                transformGroup = new TransformGroup();
                transformGroup.Children.Add(new ScaleTransform { ScaleY = -1 });
                Image.RenderTransform = transformGroup;
                Image.RenderTransformOrigin = new Point(0.5, 0.5);
            }
        }

        public void RotateRight()
        {
            if (Image.RenderTransform is RotateTransform rotateTransform)
            {
                rotateTransform.Angle += 90;
            }
            else
            {
                RotateTransform rotateTransform1 = new() { Angle = 90 };
                Image.RenderTransform = rotateTransform1;
                Image.RenderTransformOrigin = new Point(0.5, 0.5);
            }
        }

        public void RotateLeft()
        {
            if (Image.RenderTransform is RotateTransform rotateTransform)
            {
                rotateTransform.Angle -= 90;
            }
            else
            {
                RotateTransform rotateTransform1 = new() { Angle = -90 };
                Image.RenderTransform = rotateTransform1;
                Image.RenderTransformOrigin = new Point(0.5, 0.5);
            }
        }

        private ImagePlacementContext OldWindowStatus { get; set; }
        public bool IsMax { get; set; }
        public void MaxImage()
        {
            void PreviewKeyDown(object s, KeyEventArgs e)
            {
                if (e.Key == Key.Escape || e.Key == Key.F11)
                {
                    if (IsMax)
                        MaxImage();
                }
            }

            var window = Window.GetWindow(Parent);
            if (!IsMax)
            {
                IsMax = true;
                if (Parent.Parent is Panel p)
                {
                    OldWindowStatus = new ImagePlacementContext();
                    OldWindowStatus.Parent = p;
                    OldWindowStatus.WindowState = window.WindowState;
                    OldWindowStatus.WindowStyle = window.WindowStyle;
                    OldWindowStatus.ResizeMode = window.ResizeMode;
                    OldWindowStatus.Root = window.Content;
                    window.WindowStyle = WindowStyle.None;
                    window.WindowState = WindowState.Maximized;

                    OldWindowStatus.Parent.Children.Remove(Parent);
                    window.Content = Parent;

                    window.PreviewKeyDown -= PreviewKeyDown;
                    window.PreviewKeyDown += PreviewKeyDown;
                }
                else if (Parent.Parent is ContentControl content)
                {
                    OldWindowStatus = new ImagePlacementContext();
                    OldWindowStatus.ContentParent = content;
                    OldWindowStatus.WindowState = window.WindowState;
                    OldWindowStatus.WindowStyle = window.WindowStyle;
                    OldWindowStatus.ResizeMode = window.ResizeMode;
                    OldWindowStatus.Root = window.Content;
                    window.WindowStyle = WindowStyle.None;
                    window.WindowState = WindowState.Maximized;

                    content.Content = null;
                    window.Content = Parent;
                    window.PreviewKeyDown -= PreviewKeyDown;
                    window.PreviewKeyDown += PreviewKeyDown;
                    
                    return;
                }
            }
            else
            {
                IsMax =false;
                if (OldWindowStatus.Parent != null)
                {
                    window.WindowStyle = OldWindowStatus.WindowStyle;
                    window.WindowState = OldWindowStatus.WindowState;
                    window.ResizeMode = OldWindowStatus.ResizeMode;

                    window.Content = OldWindowStatus.Root;
                    OldWindowStatus.Parent.Children.Add(Parent);
                }
                else
                {
                    window.WindowStyle = OldWindowStatus.WindowStyle;
                    window.WindowState = OldWindowStatus.WindowState;
                    window.ResizeMode = OldWindowStatus.ResizeMode;

                    OldWindowStatus.ContentParent.Content = Parent;
                }
                window.PreviewKeyDown -= PreviewKeyDown;
            }
        }

        public void ClearImage()
        {
            Image.Clear();
            Image.Source = null;
            Image.UpdateLayout();

            ToolBarScaleRuler.IsShow = false;
            ClearImageEventHandler?.Invoke(this, new EventArgs());
        }

        public void SaveAs()
        {
            using var dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.Filter = "Png (*.png) | *.png";
            dialog.FileName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            dialog.RestoreDirectory = true;
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            Save(dialog.FileName);
        }

        public void Save(string FileName)
        {

            RenderTargetBitmap renderTargetBitmap = new((int)Image.ActualWidth, (int)Image.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            renderTargetBitmap.Render(Image);

            // 创建一个PngBitmapEncoder对象来保存位图为PNG文件
            PngBitmapEncoder pngEncoder = new();
            pngEncoder.Frames.Add(BitmapFrame.Create(renderTargetBitmap));

            // 将PNG内容保存到文件
            using FileStream fileStream = new(FileName, FileMode.Create);
            pngEncoder.Save(fileStream);
        }




        private void PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F11)
            {
                if (!IsMax)
                    MaxImage();
                e.Handled = true;
            }
            if (_ImageEditMode == true)
            {
                if (Keyboard.IsKeyDown(Key.LeftCtrl) && (e.Key == Key.Add || e.Key == Key.I))
                {
                    ZoomInCommand.RaiseExecute(e);
                    e.Handled = true;
                }
                else if (Keyboard.IsKeyDown(Key.LeftCtrl) && (e.Key == Key.Subtract || e.Key == Key.O))
                {
                    ZoomOutCommand.RaiseExecute(e);
                    e.Handled = true;
                }
                else if( Keyboard.IsKeyDown(Key.LeftCtrl) && (e.Key == Key.Left || e.Key == Key.A))
                {
                    TranslateTransform translateTransform = new();
                    Vector vector = new(-10, 0);
                    translateTransform.SetCurrentValue(TranslateTransform.XProperty, vector.X);
                    translateTransform.SetCurrentValue(TranslateTransform.YProperty, vector.Y);
                    ZoomboxSub.SetCurrentValue(Zoombox.ContentMatrixProperty, Matrix.Multiply(ZoomboxSub.ContentMatrix, translateTransform.Value));
                    e.Handled = true;
                }
                else if (Keyboard.IsKeyDown(Key.LeftCtrl) && (e.Key == Key.Right || e.Key == Key.D))
                {
                    TranslateTransform translateTransform = new();
                    Vector vector = new(10, 0);
                    translateTransform.SetCurrentValue(TranslateTransform.XProperty, vector.X);
                    translateTransform.SetCurrentValue(TranslateTransform.YProperty, vector.Y);
                    ZoomboxSub.SetCurrentValue(Zoombox.ContentMatrixProperty, Matrix.Multiply(ZoomboxSub.ContentMatrix, translateTransform.Value));
                    e.Handled = true;
                }
                else if (Keyboard.IsKeyDown(Key.LeftCtrl) && (e.Key == Key.Up || e.Key == Key.W))
                {
                    TranslateTransform translateTransform = new();
                    Vector vector = new(0, -10);
                    translateTransform.SetCurrentValue(TranslateTransform.XProperty, vector.X);
                    translateTransform.SetCurrentValue(TranslateTransform.YProperty, vector.Y);
                    ZoomboxSub.SetCurrentValue(Zoombox.ContentMatrixProperty, Matrix.Multiply(ZoomboxSub.ContentMatrix, translateTransform.Value));
                    e.Handled = true;
                }
                else if (Keyboard.IsKeyDown(Key.LeftCtrl) && (e.Key == Key.Down || e.Key == Key.S))
                {
                    TranslateTransform translateTransform = new();
                    Vector vector = new(0, 10);
                    translateTransform.SetCurrentValue(TranslateTransform.XProperty, vector.X);
                    translateTransform.SetCurrentValue(TranslateTransform.YProperty, vector.Y);
                    ZoomboxSub.SetCurrentValue(Zoombox.ContentMatrixProperty, Matrix.Multiply(ZoomboxSub.ContentMatrix, translateTransform.Value));
                    e.Handled = true;
                }
            }


            if (_ImageEditMode == false)
            {
                if (e.Key == Key.Add)
                {
                    ZoomInCommand.RaiseExecute(e);
                    e.Handled = true;
                }
                else if (e.Key == Key.Subtract)
                {
                    ZoomOutCommand.RaiseExecute(e);
                    e.Handled = true;
                }
                else if (e.Key == Key.Left)
                {
                    TranslateTransform translateTransform = new();
                    Vector vector = new(-10, 0);
                    translateTransform.SetCurrentValue(TranslateTransform.XProperty, vector.X);
                    translateTransform.SetCurrentValue(TranslateTransform.YProperty, vector.Y);
                    ZoomboxSub.SetCurrentValue(Zoombox.ContentMatrixProperty, Matrix.Multiply(ZoomboxSub.ContentMatrix, translateTransform.Value));
                    e.Handled = true;
                }
                else if (e.Key == Key.Right )
                {
                    TranslateTransform translateTransform = new();
                    Vector vector = new(10, 0);
                    translateTransform.SetCurrentValue(TranslateTransform.XProperty, vector.X);
                    translateTransform.SetCurrentValue(TranslateTransform.YProperty, vector.Y);
                    ZoomboxSub.SetCurrentValue(Zoombox.ContentMatrixProperty, Matrix.Multiply(ZoomboxSub.ContentMatrix, translateTransform.Value));
                    e.Handled = true;
                }
                else if (e.Key == Key.Up)
                {
                    TranslateTransform translateTransform = new();
                    Vector vector = new(0, -10);
                    translateTransform.SetCurrentValue(TranslateTransform.XProperty, vector.X);
                    translateTransform.SetCurrentValue(TranslateTransform.YProperty, vector.Y);
                    ZoomboxSub.SetCurrentValue(Zoombox.ContentMatrixProperty, Matrix.Multiply(ZoomboxSub.ContentMatrix, translateTransform.Value));
                    e.Handled = true;
                }
                else if (e.Key == Key.Down)
                {
                    TranslateTransform translateTransform = new();
                    Vector vector = new(0, 10);
                    translateTransform.SetCurrentValue(TranslateTransform.XProperty, vector.X);
                    translateTransform.SetCurrentValue(TranslateTransform.YProperty, vector.Y);
                    ZoomboxSub.SetCurrentValue(Zoombox.ContentMatrixProperty, Matrix.Multiply(ZoomboxSub.ContentMatrix, translateTransform.Value));
                    e.Handled = true;
                }
            }
        }

        public bool ScaleRulerShow
        { 
            get => ToolBarScaleRuler.IsShow;
            set
            {
                if (ToolBarScaleRuler.IsShow == value) return;
                ToolBarScaleRuler.IsShow = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 当前的缩放分辨率
        /// </summary>
        public double ZoomRatio
        {
            get => ZoomboxSub.ContentMatrix.M11;
            set => ZoomboxSub.Zoom(value);
        }

        private bool _Crosshair;
        public bool CrosshairFunction
        {
            get => _Crosshair;
            set
            {
                if (_Crosshair == value) return;
                _Crosshair = value;
                Crosshair.IsShow = value;
                OnPropertyChanged();
            }
        }

        private bool _ShowImageInfo;
        public bool ShowImageInfo
        {
            get => _ShowImageInfo; set
            {
                if (_ShowImageInfo == value) return;
                if (value) ImageEditMode = false;
                _ShowImageInfo = value;

                MouseMagnifier.IsShow = value;
                OnPropertyChanged();
            }
        }

        public EventHandler<bool> EditModeChanged { get; set; }

        private bool _ImageEditMode;

        public bool ImageEditMode
        {
            get => _ImageEditMode;
            set
            {
                if (_ImageEditMode == value) return;
                if (value) ShowImageInfo = false;
                _ImageEditMode = value;

                EditModeChanged?.Invoke(this, _ImageEditMode);

                if (_ImageEditMode)
                {
                    ZoomboxSub.ActivateOn = ModifierKeys.Control;
                    ZoomboxSub.Cursor = Cursors.Cross;
                }
                else
                {
                    ZoomboxSub.ActivateOn = ModifierKeys.None;
                    ZoomboxSub.Cursor = Cursors.Arrow;

                    LastChoice = string.Empty;
                }
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 是否画圆形
        /// </summary>
        public bool DrawCircle {
            get => CircleManager.IsShow;
            set
            {
                if (CircleManager.IsShow == value) return;
                CircleManager.IsShow = value;
                if (value)
                {
                    ImageEditMode = true;
                    LastChoice = nameof(DrawCircle);
                }
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 是否画圆形
        /// </summary>
        public bool DrawRect
        {
            get => RectangleManager.IsShow;
            set
            {
                if (RectangleManager.IsShow == value) return;
                RectangleManager.IsShow = value;
                if (value)
                {
                    ImageEditMode = true;
                    LastChoice = nameof(DrawRect);
                }
                OnPropertyChanged();
            }
        }


        public bool Measure {
            get => _Measure;
            set 
                {
                if (_Measure == value) return;
                _Measure = value;
                if (value)
                {
                    ImageEditMode = true;
                    LastChoice = nameof(Measure);
                }
                ToolBarMeasure.Measure = value;
                OnPropertyChanged();
            }
        }
        private bool _Measure;


        public bool DrawPolygon
        {
            get => PolygonManager.IsShow;
            set
            {
                if (PolygonManager.IsShow == value) return;
                PolygonManager.IsShow = value;
                if (value)
                {
                    ImageEditMode = true;
                    LastChoice = nameof(DrawPolygon);
                }

                OnPropertyChanged();
            }
        }

        public bool DrawBezierCurve
        {
            get => BezierCurveManager.IsShow;
            set
            {
                if (BezierCurveManager.IsShow == value) return;
                BezierCurveManager.IsShow = value;
                if (value)
                {
                    ImageEditMode = true;
                    LastChoice = nameof(DrawBezierCurve);
                }
                OnPropertyChanged();
            }
        }

        public bool DrawLine
        {
            get => LineManager.IsShow;
            set
            {
                if (LineManager.IsShow == value) return;
                LineManager.IsShow = value;
                if (value)
                {
                    ImageEditMode = true;
                    LastChoice = nameof(DrawLine);
                }
                OnPropertyChanged();
            }
        }




        public bool ConcentricCircle
        {
            get => ToolConcentricCircle.IsShow;
            set
            {
                if (ToolConcentricCircle.IsShow == value) return;
                if (value)
                {
                    ImageEditMode = true;
                    LastChoice = nameof(ConcentricCircle);
                }
                ToolConcentricCircle.IsShow = value;
                OnPropertyChanged();
            }
        }



        public bool GetLastChoice()
        {
            if (!string.IsNullOrWhiteSpace(_LastChoice))
            {
                Type type = GetType();
                PropertyInfo property = type.GetProperty(_LastChoice);
                if (property?.GetValue(this) is bool b)
                {
                    return b;
                }
                return false;
            }
            return false;
        }

        public string LastChoice { get => _LastChoice; set 
            {
                if (value == _LastChoice)
                    return;
                if (!string.IsNullOrWhiteSpace(_LastChoice))
                {
                    Type type = GetType();
                    PropertyInfo property = type.GetProperty(_LastChoice);
                    property?.SetValue(this, false);
                }
                _LastChoice = value;
            }
        }
        private string _LastChoice { get; set; }


        public bool EraseVisual {  get => EraseManager.IsShow;
            set
            {
                if (EraseManager.IsShow == value) return;
                EraseManager.IsShow = value;

                if (value)
                {
                    ImageEditMode = true;
                    LastChoice = nameof(EraseVisual);
                }


                if (value)
                {
                    ZoomboxSub.Cursor = Input.Cursors.Eraser;
                }
                else
                {
                    ZoomboxSub.Cursor = Cursors.Cross;
                }

                OnPropertyChanged();
            }
        }



        public void Dispose()
        {
            LineManager.Dispose();
            SelectEditorVisual.Dispose();
            CircleManager.Dispose();
            EraseManager.Dispose();
            RectangleManager.Dispose();
            PolygonManager.Dispose();
            BezierCurveManager.Dispose();
            DrawingVisualLists.Clear();
            DrawingVisualLists = null;

            Parent = null;
            ZoomboxSub = null;
            Image = null;

            GC.SuppressFinalize(this);
        }
    }
}
