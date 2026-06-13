#pragma warning disable CA1863,CS8625
using ColorVision.Common.Utilities;
using ColorVision.Core;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Annotations;
using ColorVision.ImageEditor.Draw.Ruler;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.ImageEditor.EditorTools.FullScreen;
using ColorVision.ImageEditor.Layers;
using ColorVision.ImageEditor.Properties;
using ColorVision.ImageEditor.Realtime;
using ColorVision.ImageEditor.Settings;
using ColorVision.UI;
using ColorVision.UI.Menus;
using HandyControl.Controls;
using log4net;
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
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfMessageBox = System.Windows.MessageBox;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfWindow = System.Windows.Window;

namespace ColorVision.ImageEditor
{
    /// <summary>
    /// ImageShow.xaml 的交互逻辑
    /// </summary>
    public partial class ImageView : UserControl, IDisposable, IActiveDocumentStatusProvider, INotifyPropertyChanged
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ImageView));
        private readonly DefaultImageViewDisplayConfig _defaultDisplayConfig = DefaultImageViewDisplayConfig.Current;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ImageViewConfig Config => EditorContext.Config;
        public IEditorToolFactory IEditorToolFactory => EditorContext.IEditorToolFactory;
        public IPseudoColorService PseudoColorService => EditorContext.GetRequiredService<IPseudoColorService>();
        public bool EnableEditorImageServices { get; set; } = true;
        public ImageLayerDescriptor? SelectedLayer { get; private set; }


        private RealtimeFramePresenter? _realtime;
        public RealtimeFramePresenter Realtime => _realtime ??= new RealtimeFramePresenter(this);
        private ImageFullScreenMode? _fullScreenMode;
        private WpfWindow? _shortcutWindow;

        public event EventHandler ClearImageEventHandler;
        public event EventHandler StatusBarItemsChanged;

        public EditorContext EditorContext { get; private set; } = null!;

        [DisplayName("最大缩放")]
        public double MaxZoom
        {
            get => _defaultDisplayConfig.MaxZoom;
            set
            {
                if (_defaultDisplayConfig.MaxZoom == value)
                {
                    return;
                }

                _defaultDisplayConfig.MaxZoom = value;
                OnPropertyChanged();
            }
        }

        [DisplayName("最小缩放")]
        public double MinZoom
        {
            get => _defaultDisplayConfig.MinZoom;
            set
            {
                if (_defaultDisplayConfig.MinZoom == value)
                {
                    return;
                }

                _defaultDisplayConfig.MinZoom = value;
                OnPropertyChanged();
            }
        }

        private readonly List<IImageViewSettingProvider> _imageViewSettingProviders = new();
        private readonly string _pixelValueOverlayRefreshDebounceKey = $"PixelValueOverlayRefresh_{Guid.NewGuid():N}";
        private Crosshair? _crosshair;
        private double _oldZoomRatio;
        private bool _isUpdatedRender;
        private bool _isUpdatingLayerSelection;
        private IImageLayerController? _layerController;
        private bool _isLayerSelectorEnabled = true;


        public ImageView()
        {
            InitializeComponent();
        }

        private EditorContext CreateEditorContext()
        {
            ImageViewConfig config = new();
            DrawEditorContext drawContext = new(ImageShow, Zoombox1);
            ImageProcessingContext processingContext = new(
                config,
                drawContext.DrawCanvas,
                Dispatcher,
                new ImageProcessingContextBinding
                {
                    IsInitialized = () => IsInitialized,
                    GetWidth = () => Width,
                    GetHeight = () => Height,
                    GetHImageCache = () => HImageCache,
                    SetHImageCache = value => HImageCache = value,
                    GetFunctionImage = () => FunctionImage,
                    SetFunctionImage = value => FunctionImage = value!,
                    GetViewBitmapSource = () => ViewBitmapSource,
                    SetViewBitmapSource = value => ViewBitmapSource = value!,
                    GetSelectedLayerSourceChannelIndex = GetSelectedLayerSourceChannelIndex,
                    SetImageSource = SetImageSource,
                    UpdateZoomAndScale = UpdateZoomAndScale,
                });
            return new EditorContext(
                this,
                config,
                drawContext,
                processingContext,
                TextEditorOverlay);
        }


        private void UserControl_Initialized(object sender, EventArgs e)
        {
            _defaultDisplayConfig.PropertyChanged += DefaultDisplayConfig_PropertyChanged;


            RenderOptions.SetBitmapScalingMode(ImageShow, DefaultBitmapScalingConfig.Current.DefaultBitmapScalingMode);
            EditorContext = CreateEditorContext();
            EditorContext.DrawEditorContext.SelectionVisual = new SelectEditorVisual(EditorContext.DrawEditorContext);
            EditorContext.DrawEditorContext.SelectionVisual.TextEditingContext = EditorContext.TextEditingContext;
            EditorContext.IEditorToolFactory = new IEditorToolFactory(this, EditorContext);
            EditorContext.CompactInspectorPresenter = new CompactInspectorPresenter(EditorContext);
            EditorContext.CompactInspectorPresenter.Refresh();

            ImageShow.PreviewKeyDown += HandleKeyDown;
            PreviewKeyDown += ImageView_PreviewKeyDown;
            Loaded += ImageView_Loaded;
            Unloaded += ImageView_Unloaded;
            ImageShow.ContextMenuOpening += HandleContextMenuOpening;
            ImageShow.ContextMenu = EditorContext.ContextMenu;
            ComboBoxLayers.SelectionChanged += ComboBoxLayers_SelectionChanged;
            Zoombox1.ContextMenu = EditorContext.ContextMenu;
            Zoombox1.ContentMatrixChanged += Zoombox1_ContentMatrixChanged;
            _crosshair = new Crosshair(EditorContext.DrawEditorContext);

            DataContext = this;
            this.Focusable = true;
            this.Focus();

            Config.Cleared += Config_Cleared;
            InitializeImageViewSettingProviders();

            foreach (var item in IEditorToolFactory.IImageComponents)
                item.Execute(this);

            ImageShow.VisualsAdd += ImageShow_VisualsAdd;
            ImageShow.VisualsRemove += ImageShow_VisualsRemove;
            Drop += ImageView_Drop;
            Config.ShowTextChanged += (s, e) =>
            {
                foreach (var drawingVisual in EditorContext.DrawEditorContext.DrawingVisualLists)
                {
                    if (drawingVisual.BaseAttribute is ITextProperties textProperties)
                    {
                        textProperties.IsShowText = Config.IsShowText;
                        drawingVisual.Render();
                    }
                }
            };
            Config.LayoutUpdatedChanged += (s, e) =>
            {
                ImageShow.IsLayoutUpdated = e;
                UpdateDrawingVisualScale();
                ImageShow.ApplyLayoutScaleToVisuals();
            };
            Config.DrawingTextFontSizeChanged += (s, e) =>
            {
                ImageShow.TextFontSizeOverride = e;
                ImageShow.ApplyLayoutScaleToVisuals();
            };
            Zoombox1.ContentMatrixChanged += (s, e) =>
            {
                UpdateDrawingVisualScale();
            };
            Zoombox1.LayoutUpdated += (s, e) =>
            {
                SchedulePixelValueOverlayRefresh();
            };
            Zoombox1.LayoutUpdated += (s, e) => UpdateDrawingVisualScale();
            ImageShow.IsLayoutUpdated = Config.IsLayoutUpdated;
            ImageShow.TextFontSizeOverride = Config.DrawingTextFontSize;
            UpdateDrawingVisualScale();
            SetCompactInspectorVisibility(false);
            PixelValueOverlay.Attach(this);

            Config.ShowMsgChanged += (s, e) =>
            {
                if (!e)
                {
                    foreach (var drawingVisual in EditorContext.DrawEditorContext.DrawingVisualLists)
                    {
                        drawingVisual.BaseAttribute.Msg = string.Empty;
                    }
                }
            };

            // Setup commands for file operations
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Open, (s, e) => OpenImage(), (s, e) => { e.CanExecute = true; }));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.SaveAs, (s, e) => SaveAs(), (s, e) => { e.CanExecute = true; }));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (s, e) => Clear(), (s, e) => { e.CanExecute = true; }));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Print, (s, e) => Print(), (s, e) => { e.CanExecute = true; }));

        }

        private void DefaultDisplayConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DefaultImageViewDisplayConfig.MaxZoom))
            {
                OnPropertyChanged(nameof(MaxZoom));
            }
            else if (e.PropertyName == nameof(DefaultImageViewDisplayConfig.MinZoom))
            {
                OnPropertyChanged(nameof(MinZoom));
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void Config_Cleared(object? sender, EventArgs e)
        {
            PseudoColorService?.Reset();
            FunctionImage = null;
            if (_hImageCache != null)
            {
                _hImageCache?.Dispose();
                _hImageCache = null;
            }
            GC.Collect();
        }

        public bool ImageEditMode
        {
            get => EditorContext.DrawEditorContext.IsImageEditMode;
            set => SetImageEditModeCore(value, applyUiState: true, notifyPropertyChanged: true);
        }

        private void SetImageEditModeCore(bool value, bool applyUiState, bool notifyPropertyChanged)
        {
            if (EditorContext.DrawEditorContext.IsImageEditMode == value)
            {
                return;
            }

            if (applyUiState)
            {
                Config.IsToolBarDrawVisible = value;
            }

            EditorContext.DrawEditorContext.IsImageEditMode = value;

            if (applyUiState)
            {
                if (value)
                {
                    EditorContext.DrawEditorContext.Zoombox.ActivateOn = ModifierKeys.Control;
                    EditorContext.DrawEditorContext.Zoombox.Cursor = Cursors.Cross;
                }
                else
                {
                    EditorContext.DrawEditorContext.Zoombox.ActivateOn = ModifierKeys.None;
                    EditorContext.DrawEditorContext.Zoombox.Cursor = Cursors.Arrow;
                }

                EditorContext.DrawEditorContext.DrawEditorManager.SetCurrentDrawEditor(null);
            }

            if (notifyPropertyChanged)
            {
                OnPropertyChanged(nameof(ImageEditMode));
            }
        }

        private void HandleKeyDown(object sender, KeyEventArgs e)
        {
            if (!ImageEditMode)
            {
                if (e.Key == Key.Left)
                {
                    MoveView(-10, 0);
                    e.Handled = true;
                }
                else if (e.Key == Key.Right)
                {
                    MoveView(10, 0);
                    e.Handled = true;
                }
                else if (e.Key == Key.Up)
                {
                    MoveView(0, -10);
                    e.Handled = true;
                }
                else if (e.Key == Key.Down)
                {
                    MoveView(0, 10);
                    e.Handled = true;
                }
            }
        }

        private void ImageView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.F11) return;
            ToggleFullScreen();
            e.Handled = true;
        }

        private void ImageView_Loaded(object sender, RoutedEventArgs e)
        {
            var window = WpfWindow.GetWindow(this);
            if (ReferenceEquals(_shortcutWindow, window)) return;
            if (_shortcutWindow != null) _shortcutWindow.PreviewKeyDown -= ShortcutWindow_PreviewKeyDown;
            _shortcutWindow = window;
            if (_shortcutWindow != null) _shortcutWindow.PreviewKeyDown += ShortcutWindow_PreviewKeyDown;
        }

        private void ImageView_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_shortcutWindow == null) return;
            _shortcutWindow.PreviewKeyDown -= ShortcutWindow_PreviewKeyDown;
            _shortcutWindow = null;
        }

        private void ShortcutWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.F11 || (!IsKeyboardFocusWithin && !IsMouseOver)) return;
            ToggleFullScreen();
            e.Handled = true;
        }

        public void ToggleFullScreen()
        {
            ImageContentGrid.DataContext = this;
            (_fullScreenMode ??= new ImageFullScreenMode(ImageContentGrid)).ToggleFullScreen();
        }

        private void MoveView(double x, double y)
        {
            TranslateTransform translateTransform = new();
            Vector vector = new(x, y);
            translateTransform.SetCurrentValue(TranslateTransform.XProperty, vector.X);
            translateTransform.SetCurrentValue(TranslateTransform.YProperty, vector.Y);
            EditorContext.DrawEditorContext.Zoombox.SetCurrentValue(Zoombox.ContentMatrixProperty,
                Matrix.Multiply(EditorContext.DrawEditorContext.Zoombox.ContentMatrix, translateTransform.Value));
        }

        private void HandleContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            EditorContext.ContextMenu.Items.Clear();

            if (ImageEditMode)
            {
                Point mouseDownPoint = Mouse.GetPosition(ImageShow);
                Visual? mouseVisual = ImageShow.GetVisual<Visual>(mouseDownPoint);
                if (mouseVisual == null)
                {
                    return;
                }

                if (mouseVisual is SelectEditorVisual selectEditorVisual && selectEditorVisual.GetVisual(mouseDownPoint) is ISelectVisual selectVisual)
                {
                    foreach (var provider in IEditorToolFactory.ContextMenuProviders)
                    {
                        if (provider.ContextType.IsAssignableFrom(selectVisual.GetType()))
                        {
                            var items = provider.GetContextMenuItems(selectVisual);
                            foreach (var item in items)
                            {
                                EditorContext.ContextMenu.Items.Add(item);
                            }
                        }
                    }

                    foreach (var provider in IEditorToolFactory.ContextMenuProviders)
                    {
                        if (provider.ContextType.IsAssignableFrom(selectEditorVisual.GetType()))
                        {
                            var items = provider.GetContextMenuItems(selectEditorVisual);
                            foreach (var item in items)
                            {
                                EditorContext.ContextMenu.Items.Add(item);
                            }
                        }
                    }
                }
                else
                {
                    foreach (var provider in IEditorToolFactory.ContextMenuProviders)
                    {
                        if (provider.ContextType.IsAssignableFrom(mouseVisual.GetType()))
                        {
                            var items = provider.GetContextMenuItems(mouseVisual);
                            foreach (var item in items)
                            {
                                EditorContext.ContextMenu.Items.Add(item);
                            }
                        }
                    }
                }

                if (EditorContext.ContextMenu.Items.Count == 0)
                {
                    CreateStandardContextMenu();
                }
            }
            else
            {
                CreateStandardContextMenu();
            }
        }

        private void CreateStandardContextMenu()
        {
            List<MenuItemMetadata> menuItemMetadatas = new();
            if (EditorContext.IImageOpen is IIEditorToolContextMenu contentMenuProvider)
            {
                menuItemMetadatas.AddRange(contentMenuProvider.GetContextMenuItems());
            }

            foreach (var item in IEditorToolFactory.IIEditorToolContextMenus)
            {
                if (item is IImageOpen)
                {
                    continue;
                }

                menuItemMetadatas.AddRange(item.GetContextMenuItems());
            }

            List<MenuItemMetadata> sortedMenuItems = menuItemMetadatas.OrderBy(item => item.Order).ToList();

            void CreateMenu(MenuItem parentMenuItem, string ownerGuid)
            {
                List<MenuItemMetadata> childItems = sortedMenuItems.FindAll(item => item.OwnerGuid == ownerGuid).OrderBy(item => item.Order).ToList();
                for (int i = 0; i < childItems.Count; i++)
                {
                    MenuItemMetadata childItem = childItems[i];
                    string guidId = childItem.GuidId ?? Guid.NewGuid().ToString();
                    MenuItem menuItem = new()
                    {
                        Header = childItem.Header,
                        Icon = childItem.Icon,
                        InputGestureText = childItem.InputGestureText,
                        Command = childItem.Command,
                        Tag = childItem,
                        IsChecked = childItem.IsChecked ?? false,
                        Visibility = childItem.Visibility,
                    };

                    CreateMenu(menuItem, guidId);
                    if (i > 0 && childItem.Order - childItems[i - 1].Order > 4 && childItem.Visibility == Visibility.Visible)
                    {
                        parentMenuItem.Items.Add(new Separator());
                    }

                    parentMenuItem.Items.Add(menuItem);
                }

                foreach (MenuItemMetadata item in childItems)
                {
                    sortedMenuItems.Remove(item);
                }
            }

            List<MenuItemMetadata> rootItems = menuItemMetadatas
                .Where(item => item.OwnerGuid == MenuItemConstants.Menu && item.Visibility == Visibility.Visible)
                .OrderBy(item => item.Order)
                .ToList();

            for (int i = 0; i < rootItems.Count; i++)
            {
                MenuItemMetadata menuItemMeta = rootItems[i];
                MenuItem menuItem = new()
                {
                    Header = menuItemMeta.Header,
                    Command = menuItemMeta.Command,
                    Icon = menuItemMeta.Icon,
                    InputGestureText = menuItemMeta.InputGestureText,
                    IsChecked = menuItemMeta.IsChecked ?? false,
                };

                if (menuItemMeta.GuidId != null)
                {
                    CreateMenu(menuItem, menuItemMeta.GuidId);
                }

                if (i > 0 && menuItemMeta.Order - rootItems[i - 1].Order > 4)
                {
                    EditorContext.ContextMenu.Items.Add(new Separator());
                }

                EditorContext.ContextMenu.Items.Add(menuItem);
            }
        }

        private void Zoombox1_ContentMatrixChanged(object? sender, EventArgs e)
        {
            UpdateDrawingVisualScale();

            double zoomRatio = EditorContext.DrawEditorContext.ZoomRatio;
            if (_oldZoomRatio != zoomRatio)
            {
                _oldZoomRatio = zoomRatio;
                double scale = double.IsNaN(zoomRatio) || double.IsInfinity(zoomRatio) || zoomRatio <= 0 ? 1 : 1 / zoomRatio;
                EditorContext.DrawEditorContext.DrawCanvas.Scale = scale;
                if (EditorContext.Config.IsLayoutUpdated)
                {
                    DebounceTimer.AddOrResetTimerDispatcher("ImageLayoutUpdatedRender" + EditorContext.Id, 20, () => ImageLayoutUpdatedRender(scale, EditorContext.DrawEditorContext.DrawingVisualLists));
                }
            }
        }

        private void ImageLayoutUpdatedRender(double scale, ObservableCollection<IDrawingVisual> drawingVisualLists)
        {
            if (_isUpdatedRender)
            {
                return;
            }

            try
            {
                _isUpdatedRender = true;
                EditorContext.DrawEditorContext.DrawCanvas.Scale = scale;
                EditorContext.DrawEditorContext.DrawCanvas.ApplyLayoutScaleToVisuals();
            }
            finally
            {
                _isUpdatedRender = false;
            }
        }
        private void InitializeImageViewSettingProviders()
        {
            RegisterImageViewSettingProvider(new ImageViewDisplaySettingProvider());
            RegisterImageViewSettingProvider(new ImageViewDefaultsSettingProvider());
            RegisterImageViewSettingProvider(new ImageViewWorkspaceSettingProvider());
        }

        public void RegisterImageViewSettingProvider(IImageViewSettingProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);

            if (_imageViewSettingProviders.Any(existing => existing.GetType() == provider.GetType()))
            {
                return;
            }

            _imageViewSettingProviders.Add(provider);
        }

        public IReadOnlyList<IImageViewSettingProvider> GetImageViewSettingProviders()
        {
            return _imageViewSettingProviders;
        }

        public void OpenSettingsWindow(string? initialGroup = null)
        {
            ImageViewSettingsWindow window = new(this, initialGroup)
            {
                Owner = WpfWindow.GetWindow(this) ?? Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            window.ShowDialog();
        }

        internal void SetCompactInspectorVisibility(bool isVisible)
        {
            CompactInspectorOverlay.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        internal void SetCompactInspectorItems(IEnumerable<FrameworkElement> elements)
        {
            CompactInspectorPanel.Children.Clear();

            foreach (FrameworkElement element in elements)
            {
                CompactInspectorPanel.Children.Add(element);
            }

            SetCompactInspectorVisibility(CompactInspectorPanel.Children.Count > 0);
        }


        /// <summary>
        /// 打印图像
        /// </summary>
        public void Print()
        {
            PrintDialog printDialog = new();
            if (printDialog.ShowDialog() == true)
            {
                // 创建一个可打印的区域
                Size pageSize = new(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight);
                ImageShow.Measure(pageSize);
                ImageShow.Arrange(new Rect(5, 5, pageSize.Width, pageSize.Height));

                // 开始打印
                printDialog.PrintVisual(ImageShow, "Printing");
            }
        }

        internal void SchedulePixelValueOverlayRefresh()
        {
            if (RenderOptions.GetBitmapScalingMode(ImageShow) == BitmapScalingMode.NearestNeighbor)
            {
                DebounceTimer.AddOrResetTimerDispatcher(_pixelValueOverlayRefreshDebounceKey, 24, RefreshPixelValueOverlay);
            }
        }


        private void RefreshPixelValueOverlay()
        {
            PixelValueOverlay.Refresh();
        }

        public void OpenImage()
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                OpenImage(openFileDialog.FileName);
            }
        }

        /// <summary>
        /// 显示"另存为"对话框
        /// </summary>
        public void SaveAs()
        {
            using var dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.Filter = "Png (*.png) | *.png";
            dialog.FileName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            dialog.RestoreDirectory = true;
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            Save(dialog.FileName);
        }

        /// <summary>
        /// 保存图像到指定文件
        /// </summary>
        /// <param name="fileName">文件路径</param>
        public void Save(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                log.Warn("Skip saving ImageView because file name is empty.");
                return;
            }

            ImageShow.UpdateLayout();
            int pixelWidth = GetRenderPixelLength(ImageShow.ActualWidth, ImageShow.RenderSize.Width);
            int pixelHeight = GetRenderPixelLength(ImageShow.ActualHeight, ImageShow.RenderSize.Height);
            if (pixelWidth <= 0 || pixelHeight <= 0)
            {
                log.WarnFormat(
                    "Skip saving ImageView because render size is invalid. File={0}, Actual={1}x{2}, RenderSize={3}x{4}, Source={5}",
                    fileName,
                    ImageShow.ActualWidth,
                    ImageShow.ActualHeight,
                    ImageShow.RenderSize.Width,
                    ImageShow.RenderSize.Height,
                    ImageShow.Source?.GetType().FullName ?? "<null>");
                return;
            }

            double dpiX = GetPositiveDpi(Config.GetProperties<double>("DpiX"));
            double dpiY = GetPositiveDpi(Config.GetProperties<double>("DpiY"));

            string? directory = Path.GetDirectoryName(fileName);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            RenderTargetBitmap renderTargetBitmap = new(pixelWidth, pixelHeight, dpiX, dpiY, PixelFormats.Pbgra32);
            renderTargetBitmap.Render(ImageShow);

            // 创建一个PngBitmapEncoder对象来保存位图为PNG文件
            PngBitmapEncoder pngEncoder = new();
            pngEncoder.Frames.Add(BitmapFrame.Create(renderTargetBitmap));

            // 将PNG内容保存到文件
            using FileStream fileStream = new(fileName, FileMode.Create);
            pngEncoder.Save(fileStream);
        }

        private static int GetRenderPixelLength(params double[] values)
        {
            foreach (double value in values)
            {
                if (IsPositiveFinite(value))
                {
                    return Math.Max(1, (int)Math.Ceiling(value));
                }
            }

            return 0;
        }

        private static double GetPositiveDpi(double value)
        {
            return IsPositiveFinite(value) ? value : 96d;
        }

        private static bool IsPositiveFinite(double value)
        {
            return value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        public void ClearAnnotations()
        {
            foreach (Visual visual in EditorContext.DrawEditorContext.DrawingVisualLists.OfType<Visual>().ToList())
            {
                ImageShow.RemoveVisual(visual);
            }
        }

        public void ExportAnnotations()
        {
            List<DrawingVisualBase> visuals = EditorContext.DrawEditorContext.DrawingVisualLists.OfType<DrawingVisualBase>().ToList();
            if (visuals.Count == 0)
            {
                WpfMessageBox.Show(Properties.Resources.ImageView_NoExportableAnnotations, Properties.Resources.ImageView_ExportAnnotations, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            AnnotationDocument document = AnnotationMapper.CreateDocument(visuals);
            if (document.Items.Count == 0)
            {
                WpfMessageBox.Show(Properties.Resources.ImageView_NoAnnotationTypes, Properties.Resources.ImageView_ExportAnnotations, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            using var dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.Filter = "Annotation Files (*.cvanno.json)|*.cvanno.json|JSON Files (*.json)|*.json|All Files (*.*)|*.*";
            dialog.DefaultExt = "cvanno.json";
            dialog.AddExtension = true;
            dialog.RestoreDirectory = true;
            dialog.FileName = "annotations-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            File.WriteAllText(dialog.FileName, AnnotationMapper.Serialize(document));

            int skippedCount = visuals.Count - document.Items.Count;
            string message = skippedCount > 0
                ? string.Format(Properties.Resources.ImageView_ExportedAnnotationsWithSkip, document.Items.Count, skippedCount)
                : string.Format(Properties.Resources.ImageView_ExportedAnnotations, document.Items.Count);
            WpfMessageBox.Show(message, Properties.Resources.ImageView_ExportAnnotations, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ImportAnnotations()
        {
            using var dialog = new System.Windows.Forms.OpenFileDialog();
            dialog.Filter = "Annotation Files (*.cvanno.json)|*.cvanno.json|JSON Files (*.json)|*.json|All Files (*.*)|*.*";
            dialog.RestoreDirectory = true;
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            try
            {
                string json = File.ReadAllText(dialog.FileName);
                AnnotationDocument document = AnnotationMapper.Deserialize(json);
                IReadOnlyList<DrawingVisualBase> visuals = AnnotationMapper.ToVisuals(document);

                ClearAnnotations();
                foreach (DrawingVisualBase visual in visuals)
                {
                    visual.Render();
                    ImageShow.AddVisual(visual);
                }

                WpfMessageBox.Show(string.Format(Properties.Resources.ImageView_ImportedAnnotations, visuals.Count), Properties.Resources.ImageView_ImportAnnotations, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show(string.Format(Properties.Resources.ImageView_ImportAnnotationsFailed, ex.Message), Properties.Resources.ImageView_ImportAnnotations, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImageShow_VisualsAdd(object? sender, VisualChangedEventArgs e)
        {
            if (e.Visual is IDrawingVisual visual && !EditorContext.DrawEditorContext.DrawingVisualLists.Contains(visual) && sender is Visual visual1)
            {
                EditorContext.DrawEditorContext.DrawingVisualLists.Add(visual);
            }

        }

        private void ImageShow_VisualsRemove(object? sender, VisualChangedEventArgs e)
        {
            if (e.Visual is IDrawingVisual visual)
                EditorContext.DrawEditorContext.DrawingVisualLists.Remove(visual);
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
                    e.Handled = true;
                }
            }
        }

        public void Clear()
        {
            InvalidatePseudoColorRender();
            ClearImageEventHandler?.Invoke(this, new EventArgs());
            EditorContext.IImageOpen = null;
            IEditorToolFactory.ApplyImageOpenTools(null);
            SetLayerController(null);
            Config.ClearProperties();
            FunctionImage = null;
            ViewBitmapSource = null;
            ImageShow.Clear();
            ImageShow.Source = null;
            ImageShow.UpdateLayout();
        }

        public IEnumerable<StatusBarMeta> GetActiveStatusBarItems()
        {
            var items = new List<StatusBarMeta>();

            var cols = Config.GetProperties<int>("Cols");
            var rows = Config.GetProperties<int>("Rows");
            if (cols > 0 && rows > 0)
            {
                items.Add(new StatusBarMeta
                {
                    Id = "ImageDimensions",
                    Name = "Image Size",
                    Description = $"{cols} x {rows}",
                    Type = StatusBarType.Text,
                    Alignment = StatusBarAlignment.Right,
                    Order = 100,
                    Source = this,
                });
                // 使用直接赋值内容，因为 Properties dict 不是 INotifyPropertyChanged 可绑定的
                items[^1].BindingName = null; // 不绑定，使用 Description 显示
            }

            var channel = Config.GetProperties<int>("Channel");
            var depth = Config.GetProperties<int>("Depth");
            if (channel > 0)
            {
                items.Add(new StatusBarMeta
                {
                    Id = "ImageFormat",
                    Name = "Format",
                    Description = $"Ch:{channel} Depth:{depth}bit",
                    Type = StatusBarType.Text,
                    Alignment = StatusBarAlignment.Right,
                    Order = 101,
                    Source = this,
                });
            }

            var filePath = Config.FilePath;
            if (!string.IsNullOrEmpty(filePath))
            {
                var ext = Path.GetExtension(filePath).ToUpperInvariant();
                items.Add(new StatusBarMeta
                {
                    Id = "ImageFileType",
                    Name = "File Type",
                    Description = ext,
                    Type = StatusBarType.Text,
                    Alignment = StatusBarAlignment.Right,
                    Order = 102,
                    Source = this,
                });
            }

            return items;
        }

        public void OpenImage(WriteableBitmap? writeableBitmap)
        {
            if (writeableBitmap != null)
            {
                SetImageSource(writeableBitmap);
            }
            else
            {
                log.Error("传入的 WriteableBitmap 为 null，无法打开图像。");
            }
        }

        public void OpenImage(string? filePath)
        {
            //如果文件已经打开，不会重复打开

            if (filePath == null || filePath.Equals(Config.GetProperties<string>(ImageViewPropertyKeys.FilePath), StringComparison.Ordinal))
            {
                log.Info("文件路径未改变，跳过打开图像。");
                return;
            }
            Config.ClearProperties();
            EditorContext.IImageOpen = null;
            IEditorToolFactory.ApplyImageOpenTools(null);
            SetLayerController(null);
            Config.SetImageMetadata(ImageViewPropertyKeys.FilePath, filePath, nameof(ImageView), Properties.Resources.ImageView_MetadataDesc_FilePath);
            try
            {
                if (filePath != null && File.Exists(filePath))
                {
                    long fileSize = new FileInfo(filePath).Length;
                    Config.SetImageMetadata(ImageViewPropertyKeys.FileSize, fileSize, nameof(ImageView), Properties.Resources.ImageView_MetadataDesc_FileSize);

                    string ext = Path.GetExtension(filePath).ToLower(CultureInfo.CurrentCulture);
                    if (IEditorToolFactory.IImageOpens.TryGetValue(ext, out var imageOpen))
                    {
                        EditorContext.IImageOpen = imageOpen;
                        EditorContext.IImageOpen.OpenImage(EditorContext, filePath);
                        IEditorToolFactory.ApplyImageOpenTools(imageOpen);
                        return;
                    }
                    else
                    {
                        WpfMessageBox.Show(string.Format(Properties.Resources.ImageView_UnsupportedImageFormat, ext));
                    }
                }
            }
            catch(Exception ex)
            {
                EditorContext.IImageOpen = null;
                IEditorToolFactory.ApplyImageOpenTools(null);
                log.Error(ex);
                WpfMessageBox.Show(ex.Message);
            }
        }


        public HImage? HImageCache
        {
            get
            {
                if (_hImageCache == null)
                {
                    if (ImageShow.CheckAccess())
                    {
                        ViewBitmapSource = ImageShow.Source;

                        if (ImageShow.Source is WriteableBitmap writeableBitmap)
                        {
                            _hImageCache = writeableBitmap.ToHImage();
                        }
                    }
                    else
                    {
                        ImageShow.Dispatcher.Invoke(() =>
                        {
                            ViewBitmapSource = ImageShow.Source;
                            if (ImageShow.Source is WriteableBitmap writeableBitmap)
                            {
                                _hImageCache = writeableBitmap.ToHImage();
                            }
                        });
                    }
                }
                return _hImageCache;
            }
            set { _hImageCache?.Dispose(); _hImageCache = value; }
        }
        private HImage? _hImageCache;


        public void SetImageSource(ImageSource imageSource)
        {
            SetImageSource(imageSource, EnableEditorImageServices, true);
        }


        public void SetImageSource(ImageSource imageSource, bool enableEditorImageServices, bool configureDefaultLayerController)
        {
            PseudoColorService?.Reset();
            InvalidatePseudoColorRender();
            FunctionImage = null;
            ViewBitmapSource = null;
            ImageShow.Source = null;
            if (HImageCache != null)
            {
                HImageCache = null;
            }

            _isLayerSelectorEnabled = enableEditorImageServices;
            if (imageSource is WriteableBitmap writeableBitmap)
            {
                int cols = writeableBitmap.PixelWidth;
                int rows = writeableBitmap.PixelHeight;
                int channels, depth;

                switch (writeableBitmap.Format.ToString())
                {
                    case "Bgr32":
                    case "Bgra32":
                    case "Pbgra32":
                        channels = 4; // BGRA format has 4 channels
                        depth = 8; // 8 bits per channel
                        break;
                    case "Bgr24":
                    case "Rgb24":
                        channels = 3; // RGB format has 3 channels
                        depth = 8; // 8 bits per channel
                        break;
                    case "Indexed8":
                        depth = 8; // 8 bits per channel
                        channels = 1;
                        break;
                    case "Rgb48":
                        channels = 3; // RGB format has 3 channels
                        depth = 16; // 8 bits per channel
                        break;
                    case "Gray8":
                        channels = 1; // Gray scale has 1 channel
                        depth = 8; // 8 bits per channel
                        break;
                    case "Gray16":
                        channels = 1; // Gray scale has 1 channel
                        depth = 16; // 16 bits per channel
                        break;
                    case "Gray32Float":
                        channels = 1; // Gray scale has 1 channel
                        depth = 32; // 16 bits per channel
                        break;
                    default:
                        WpfMessageBox.Show(string.Format(Properties.Resources.ImageView_UnsupportedPixelFormat, writeableBitmap.Format));
                        throw new NotSupportedException("The pixel format is not supported.");
                }

                int stride = cols * channels * (depth / 8);

                Config.SetImageMetadata(ImageViewPropertyKeys.PixelFormat, writeableBitmap.Format, nameof(ImageView), Properties.Resources.ImageView_MetadataDesc_PixelFormat);
                Config.SetImageMetadata(ImageViewPropertyKeys.Cols, cols, nameof(ImageView), Properties.Resources.ImageView_MetadataDesc_Cols);
                Config.SetImageMetadata(ImageViewPropertyKeys.Rows, rows, nameof(ImageView), Properties.Resources.ImageView_MetadataDesc_Rows);
                Config.SetImageMetadata(ImageViewPropertyKeys.Channel, channels, nameof(ImageView), Properties.Resources.ImageView_MetadataDesc_Channel);
                Config.SetImageMetadata(ImageViewPropertyKeys.Depth, depth, nameof(ImageView), Properties.Resources.ImageView_MetadataDesc_Depth);
                Config.SetImageMetadata(ImageViewPropertyKeys.Stride, stride, nameof(ImageView), Properties.Resources.ImageView_MetadataDesc_Stride);
                Config.SetImageMetadata(ImageViewPropertyKeys.DpiX, writeableBitmap.DpiX, nameof(ImageView), Properties.Resources.ImageView_MetadataDesc_DpiX);
                Config.SetImageMetadata(ImageViewPropertyKeys.DpiY, writeableBitmap.DpiY, nameof(ImageView), Properties.Resources.ImageView_MetadataDesc_DpiY);
                if (enableEditorImageServices)
                {
                    PseudoColorService?.ConfigureForImage();
                }
            }

            if (enableEditorImageServices)
            {
                ImageCalibrationService.ApplyToDefault(Config);
            }

            ViewBitmapSource = imageSource;
            ImageShow.Source = ViewBitmapSource;
            if (configureDefaultLayerController)
            {
                SetLayerController(BitmapImageLayerController.CreateForCurrentImage(this));
            }
            else
            {
                UpdateLayerSelectorVisibility();
            }
            ImageShow.RaiseImageInitialized();
            CommandManager.InvalidateRequerySuggested();

            // 图像加载完成后通知状态栏刷新
            StatusBarItemsChanged?.Invoke(this, EventArgs.Empty);

        }

        public ImageSource FunctionImage { get; set; }
        public ImageSource ViewBitmapSource { get; set; }

        public void SetLayerController(IImageLayerController? controller)
        {
            _layerController = controller;
            _isUpdatingLayerSelection = true;
            try
            {
                ComboBoxLayers.ItemsSource = controller?.Layers;
                SelectedLayer = controller?.DefaultLayer;
                ComboBoxLayers.SelectedItem = SelectedLayer;

                if (SelectedLayer == null && controller != null && controller.Layers.Count > 0)
                {
                    SelectedLayer = controller.Layers[0];
                    ComboBoxLayers.SelectedItem = SelectedLayer;
                }
            }
            finally
            {
                _isUpdatingLayerSelection = false;
            }

            UpdateLayerSelectorVisibility();
        }

        public int GetSelectedLayerSourceChannelIndex()
        {
            return SelectedLayer?.SourceChannelIndex ?? -1;
        }

        private void ComboBoxLayers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingLayerSelection || _layerController == null)
            {
                return;
            }

            if (ComboBoxLayers.SelectedItem is not ImageLayerDescriptor layer)
            {
                return;
            }

            SelectedLayer = layer;
            _layerController.SelectLayer(layer);
        }

        private void UpdateLayerSelectorVisibility()
        {
            bool hasMultipleLayers = _layerController != null && _layerController.Layers.Count > 1;
            ComboBoxLayers.Visibility = _isLayerSelectorEnabled && hasMultipleLayers ? Visibility.Visible : Visibility.Collapsed;
        }

        public void AddVisual(Visual visual)
        {
            ImageShow.AddVisualCommand(visual);
        }

        private void InvalidatePseudoColorRender()
        {
            PseudoColorService?.Invalidate();
        }

        public void ExtractChannel(int channel)
        {
            if (ViewBitmapSource == null) return;

            if (channel == -1)
            {
                ImageShow.Source = ViewBitmapSource;
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
                            hImageProcessed.Dispose();
                            FunctionImage = image;
                        }
                        ImageShow.Source = FunctionImage;
                    }
                });
            });

        }
        public void UpdateZoomAndScale()
        {
            if (CheckAccess())
            {
                UpdateZoomAndScaleCore();
            }
            else
            {
                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    UpdateZoomAndScaleCore();
                });
            }
        }

        private void UpdateZoomAndScaleCore()
        {
            Zoombox1.ZoomUniform();
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                UpdateDrawingVisualScale();
                ImageShow.ApplyLayoutScaleToVisuals();
            }));
        }

        private void UpdateDrawingVisualScale()
        {
            ImageShow.Scale = GetDrawingVisualScale();
        }

        private double GetDrawingVisualScale()
        {
            double zoomRatio = EditorContext?.DrawEditorContext.ZoomRatio ?? 1;
            return double.IsNaN(zoomRatio) || double.IsInfinity(zoomRatio) || zoomRatio <= 0 ? 1 : 1 / zoomRatio;
        }


        public void ApplyCurrentImage()
        {
            InvalidatePseudoColorRender();
            if (FunctionImage is WriteableBitmap writeableBitmap)
            {
                ViewBitmapSource = writeableBitmap;
                ImageShow.Source = ViewBitmapSource; ;
                HImageCache = writeableBitmap.ToHImage();
                FunctionImage = null;
                SetLayerController(BitmapImageLayerController.CreateForCurrentImage(this));
            }
        }

        public void ReloadImage()
        {
            InvalidatePseudoColorRender();
            string filepath = Config.FilePath;
            Config.ClearProperties();
            OpenImage(filepath);
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            ApplyCurrentImage();
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            ReloadImage();
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is WpfTextBox textBox && textBox.AcceptsReturn)
            {
                return;
            }

            if (e.Key == Key.Enter)
            {
                Common.NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }


        #region Transient Select Mode

        /// <summary>
        /// Start a transient (non-recording) selection mode on the current image.
        /// The user draws a single shape inline; on mouse-up the mode ends and returns the result.
        /// For Polygon mode: each click adds a point; press Enter/Space to complete, Escape to cancel.
        /// Returns null if cancelled (Escape) or too-small selection.
        /// </summary>
        /// <param name="shapeType">Rectangle, Circle, or Polygon</param>
        /// <returns>SelectResult with shape properties, or null if cancelled</returns>
        public Task<SelectResult> BeginSelectAsync(SelectShapeType shapeType)
        {
            var mode = new TransientRoiSelectionSession(EditorContext.DrawEditorContext, shapeType);
            return mode.Start();
        }

        #endregion

        public void Dispose()
        {
            _realtime?.Dispose();
            Clear();
            _defaultDisplayConfig.PropertyChanged -= DefaultDisplayConfig_PropertyChanged;
            Config.Cleared -= Config_Cleared;
            IEditorToolFactory.Dispose();
            EditorContext.DrawEditorContext.MouseInfoProvider.Dispose();
            EditorContext.CompactInspectorPresenter?.Dispose();
            EditorContext.DrawEditorContext.DrawingVisualLists?.Clear();
            Zoombox1.ContentMatrixChanged -= Zoombox1_ContentMatrixChanged;
            Loaded -= ImageView_Loaded;
            Unloaded -= ImageView_Unloaded;
            if (_shortcutWindow != null) _shortcutWindow.PreviewKeyDown -= ShortcutWindow_PreviewKeyDown;
            PreviewKeyDown -= ImageView_PreviewKeyDown;
            ImageShow.PreviewKeyDown -= HandleKeyDown;
            ImageShow.ContextMenuOpening -= HandleContextMenuOpening;
            ImageShow.VisualsAdd -= ImageShow_VisualsAdd;
            ImageShow.VisualsRemove -= ImageShow_VisualsRemove;
            ComboBoxLayers.SelectionChanged -= ComboBoxLayers_SelectionChanged;

            ImageShow.Dispose();
            Drop -= ImageView_Drop;

            Zoombox1.Child = null;
            ZoomGrid.Children.Clear();
            GC.Collect();
            GC.SuppressFinalize(this);
        }

    }
}
