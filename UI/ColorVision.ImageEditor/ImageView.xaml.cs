#pragma warning disable CA1863,CS8625
using ColorVision.Common.Utilities;
using ColorVision.Core;
using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Annotations;
using ColorVision.ImageEditor.Draw.Ruler;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.ImageEditor.EditorTools.FullScreen;
using ColorVision.ImageEditor.Layers;
using ColorVision.ImageEditor.Navigation;
using ColorVision.ImageEditor.Tooling;
using ColorVision.ImageEditor.Realtime;
using ColorVision.ImageEditor.Settings;
using ColorVision.ImageEditor.Documents;
using ColorVision.ImageEditor.Presentation;
using ColorVision.UI;
using ColorVision.UI.Menus;
using log4net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
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
        private readonly ImageDocument _document;
        private readonly ImageEditorSession _session;
        private ImagePresentation _presentation = null!;
        private ImageChannelPresenter _channels = null!;
        private ImageDrawingPresentation _drawing = null!;
        private ImageContextMenuComposer _contextMenus = null!;
        private readonly List<Func<IEnumerable<ImageViewSettingsEntry>>> _settingsEntries = new();
        private int _disposed;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ImageViewConfig Config => EditorContext.Config;
        public IEditorToolFactory IEditorToolFactory => EditorContext.IEditorToolFactory;
        public ImageDocument Document => _document;
        public ImagePresentation Presentation => _presentation;
        public bool EnableEditorImageServices { get; set; } = true;
        public ImageLayerDescriptor? SelectedLayer { get; private set; }
        public bool AutoFollowImageGroup
        {
            get => _imageGroup.AutoFollow;
            set => _imageGroup.AutoFollow = value;
        }
        public IReadOnlyList<ImageViewImageItem> ImageGroupItems => _imageGroup.Items;
        public int SelectedImageIndex => _imageGroup.SelectedIndex;

        private readonly ImageGroupNavigation _imageGroup;

        private RealtimeFramePresenter? _realtime;
        public RealtimeFramePresenter Realtime => _realtime ??= new RealtimeFramePresenter(this);
        private ImageFullScreenMode? _fullScreenMode;
        private Matrix _windowedImageMatrix;
        private WpfWindow? _shortcutWindow;

        public event EventHandler ClearImageEventHandler;
        public event EventHandler StatusBarItemsChanged;
        public event EventHandler<ImageViewImageChangedEventArgs>? SelectedImageChanged;

        /// <summary>Raised after a new pixel source is assigned to this view.</summary>
        public event EventHandler<ImageViewImageSourceLoadedEventArgs>? ImageSourceLoaded;

        /// <summary>
        /// Raised only when an external renderer explicitly reports that it has finished updating the scene.
        /// </summary>
        public event EventHandler<ImageViewExternalRenderCompletedEventArgs>? ExternalRenderCompleted;

        /// <summary>Publishes that the current pixel source was loaded or updated in place.</summary>
        public void NotifyImageSourceLoaded()
        {
            Dispatcher.VerifyAccess();
            ImageSource? source = ViewBitmapSource ?? ImageShow.Source;
            if (source == null)
                return;

            ImageShow.RaiseImageInitialized();
            ImageSourceLoaded?.Invoke(
                this,
                new ImageViewImageSourceLoadedEventArgs(source, ImageRevision));
        }

        /// <summary>Notifies listeners that external scene rendering for the current image has finished.</summary>
        public void NotifyExternalRenderCompleted(object? context = null, bool succeeded = true)
        {
            Dispatcher.VerifyAccess();
            ImageSource? source = ViewBitmapSource ?? ImageShow.Source;
            ExternalRenderCompleted?.Invoke(
                this,
                new ImageViewExternalRenderCompletedEventArgs(
                    source,
                    ImageRevision,
                    context,
                    succeeded));
        }

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

        private readonly string _pixelValueOverlayRefreshDebounceKey = $"PixelValueOverlayRefresh_{Guid.NewGuid():N}";
        private Crosshair? _crosshair;
        private bool _isUpdatingLayerSelection;
        private IImageLayerController? _layerController;
        private bool _isLayerSelectorEnabled = true;


        private readonly AlgorithmRuntime _algorithmRuntime;

        internal Action<AlgorithmInvocationClaim>? AlgorithmClaimStateUpdateHook { get; set; }

        internal Action<AlgorithmInvocationClaim>? AlgorithmPreviewPublicationHook { get; set; }

        internal Action<AlgorithmInvocationClaim>? AlgorithmPreviewCommitHook { get; set; }

        internal Action<AlgorithmInvocationClaim>? AlgorithmPreviewClaimAcceptedHook { get; set; }

        internal Action<long, long>? ImageDocumentRevisionAdvancedHook { get; set; }

        public ImageView()
            : this(ImageAlgorithmPlatform.Runtime)
        {
        }

        public ImageView(AlgorithmRuntime algorithmRuntime)
        {
            _algorithmRuntime = algorithmRuntime ?? throw new ArgumentNullException(nameof(algorithmRuntime));
            _document = new ImageDocument(Dispatcher);
            _session = new ImageEditorSession(_document, (previous, current) => ImageDocumentRevisionAdvancedHook?.Invoke(previous, current));
            _imageGroup = new ImageGroupNavigation(path => OpenImageCore(path));
            _imageGroup.Changed += (_, _) => UpdateImageGroupNavigator();
            _imageGroup.SelectedImageChanged += (_, change) => SelectedImageChanged?.Invoke(this, change);
            InitializeComponent();
        }

        private EditorContext CreateEditorContext()
        {
            ImageViewConfig config = ImageEditorSession.CreateConfiguration();
            DrawEditorContext drawContext = new(ImageShow, Zoombox1);
            _presentation = new ImagePresentation(_document, ImageShow);
            ImageProcessingContext processingContext = new(
                config,
                drawContext.DrawCanvas,
                Dispatcher,
                new ImageProcessingContextBinding
                {
                    IsInitialized = () => IsInitialized,
                    GetDocumentInstanceId = () => _document.Id,
                    IsDisposed = () => Volatile.Read(ref _disposed) != 0,
                    GetImageRevision = () => _document.Revision,
                    AcquireImageFrame = AcquireImageFrame,
                    IsCurrentImageRevision = IsCurrentImageRevision,
                    NotifySourcePixelsChanged = NotifySourcePixelsChanged,
                    CommitSourcePixels = _session.CommitSourcePixels,
                    GetViewBitmapSource = () => _document.Source,
                    SetViewBitmapSource = _document.AssignSource,
                    GetSelectedLayerSourceChannelIndex = GetSelectedLayerSourceChannelIndex,
                    SetImageSource = SetImageSource,
                    UpdateZoomAndScale = UpdateZoomAndScale,
                    BeforeAlgorithmClaimStateUpdate = claim => AlgorithmClaimStateUpdateHook?.Invoke(claim),
                    BeforeAlgorithmPreviewPublication = claim => AlgorithmPreviewPublicationHook?.Invoke(claim),
                    BeforeAlgorithmPreviewCommit = claim => AlgorithmPreviewCommitHook?.Invoke(claim),
                    AfterAlgorithmPreviewClaimAccepted = claim => AlgorithmPreviewClaimAcceptedHook?.Invoke(claim),
                },
                _algorithmRuntime,
                _presentation);
            _session.Attach(processingContext);
            _channels = new ImageChannelPresenter(processingContext);
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
            _drawing = new ImageDrawingPresentation(EditorContext.DrawEditorContext, Config);
            EditorContext.DrawEditorContext.SelectionVisual = new SelectEditorVisual(EditorContext.DrawEditorContext);
            EditorContext.DrawEditorContext.SelectionVisual.TextEditingContext = EditorContext.TextEditingContext;
            EditorContext.IEditorToolFactory = new IEditorToolFactory(this, EditorContext);
            _contextMenus = new ImageContextMenuComposer(EditorContext);
            EditorContext.CompactInspectorPresenter = new CompactInspectorPresenter(EditorContext);
            EditorContext.CompactInspectorPresenter.Refresh();

            ImageShow.PreviewKeyDown += HandleKeyDown;
            PreviewKeyDown += ImageView_PreviewKeyDown;
            Loaded += ImageView_Loaded;
            Unloaded += ImageView_Unloaded;
            ImageShow.ContextMenu = EditorContext.ContextMenu;
            ComboBoxLayers.SelectionChanged += ComboBoxLayers_SelectionChanged;
            Zoombox1.ContextMenuOpening += _contextMenus.HandleOpening;
            Zoombox1.ContextMenu = EditorContext.ContextMenu;
            _crosshair = new Crosshair(EditorContext.DrawEditorContext);

            DataContext = this;
            this.Focusable = true;
            this.Focus();

            Config.Cleared += Config_Cleared;
            foreach (var item in IEditorToolFactory.IImageComponents)
                item.Execute(this);

            _drawing.Attach();
            Drop += ImageView_Drop;
            Zoombox1.LayoutUpdated += Zoombox1_LayoutUpdated;
            SetCompactInspectorVisibility(false);
            PixelValueOverlay.Attach(this);

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
            _session.OnConfigurationCleared();
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
            if (e.Handled || e.Key != Key.F11 || Keyboard.Modifiers != ModifierKeys.None) return;
            e.Handled = true;
            if (!e.IsRepeat) ToggleFullScreen();
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
            if (e.Handled || e.Key != Key.F11 || Keyboard.Modifiers != ModifierKeys.None ||
                WindowFullScreenSession.GetIsActive((WpfWindow)sender) || (!IsKeyboardFocusWithin && !IsMouseOver)) return;
            e.Handled = true;
            if (!e.IsRepeat) ToggleFullScreen();
        }

        public void ToggleFullScreen()
        {
            ImageContentGrid.DataContext = this;
            if (_fullScreenMode == null)
            {
                _fullScreenMode = new ImageFullScreenMode(ImageContentGrid);
                _fullScreenMode.FullScreenChanged += (_, _) =>
                {
                    if (_fullScreenMode.IsMax) Zoombox1.ZoomUniform();
                    else Zoombox1.RestoreView(_windowedImageMatrix);
                };
            }
            if (!_fullScreenMode.IsMax) _windowedImageMatrix = Zoombox1.ContentMatrix;
            _fullScreenMode.ToggleFullScreen();
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

        private void Zoombox1_LayoutUpdated(object? sender, EventArgs e) => SchedulePixelValueOverlayRefresh();

        public void RegisterSettings(Func<IEnumerable<ImageViewSettingsEntry>> getEntries)
        {
            RegisterSettingsProvider(getEntries);
        }

        public IDisposable RegisterSettingsProvider(Func<IEnumerable<ImageViewSettingsEntry>> getEntries)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            ArgumentNullException.ThrowIfNull(getEntries);
            _settingsEntries.Add(getEntries);
            return new SettingsRegistration(_settingsEntries, getEntries);
        }

        internal IEnumerable<ImageViewSettingsEntry> GetRegisteredSettings()
        {
            var entries = new List<ImageViewSettingsEntry>();
            foreach (var provider in _settingsEntries.ToArray())
            {
                try { entries.AddRange(provider().Where(entry => entry != null && entry.Source != null).ToArray()); }
                catch (Exception ex) { log.Warn("Image settings provider failed.", ex); }
            }
            return entries;
        }

        private sealed class SettingsRegistration(List<Func<IEnumerable<ImageViewSettingsEntry>>> providers, Func<IEnumerable<ImageViewSettingsEntry>> provider) : IDisposable
        {
            private bool _disposed;
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                providers.Remove(provider);
            }
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

            CommitActiveDrawingEditsForOutput();
            visuals = EditorContext.DrawEditorContext.DrawingVisualLists.OfType<DrawingVisualBase>().ToList();
            document = AnnotationMapper.CreateDocument(visuals);
            if (document.Items.Count == 0)
            {
                WpfMessageBox.Show(Properties.Resources.ImageView_NoAnnotationTypes, Properties.Resources.ImageView_ExportAnnotations, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            File.WriteAllText(dialog.FileName, AnnotationMapper.Serialize(document));

            int skippedCount = visuals.Count - document.Items.Count;
            string message = skippedCount > 0
                ? string.Format(Properties.Resources.ImageView_ExportedAnnotationsWithSkip, document.Items.Count, skippedCount)
                : string.Format(Properties.Resources.ImageView_ExportedAnnotations, document.Items.Count);
            WpfMessageBox.Show(message, Properties.Resources.ImageView_ExportAnnotations, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CommitActiveDrawingEditsForOutput() => _drawing.CommitActiveEdits();

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





        public void OpenImages(IEnumerable<string>? filePaths, int selectedIndex = 0)
        {
            if (!Dispatcher.CheckAccess())
            {
                var paths = filePaths?.ToList();
                Dispatcher.BeginInvoke(() => OpenImages(paths, selectedIndex));
                return;
            }

            var items = filePaths?
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => new ImageViewImageItem(path))
                .ToList() ?? new List<ImageViewImageItem>();

            OpenImageGroup(items, selectedIndex);
        }

        public void OpenImageGroup(IEnumerable<ImageViewImageItem>? images, int selectedIndex = 0)
        {
            if (!Dispatcher.CheckAccess())
            {
                var imageList = images?.ToList();
                Dispatcher.BeginInvoke(() => OpenImageGroup(imageList, selectedIndex));
                return;
            }

            if (!_imageGroup.TryOpenGroup(images, selectedIndex)) Clear();
        }

        public void AppendImage(string? filePath, bool open = true)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(() => AppendImage(filePath, open));
                return;
            }

            _imageGroup.Append(filePath, open);
        }

        public void ClearImageGroup()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(ClearImageGroup);
                return;
            }

            _imageGroup.Clear();
        }

        public void SelectImage(int index)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(() => SelectImage(index));
                return;
            }

            _imageGroup.Select(index);
        }

        private void UpdateImageGroupNavigator()
        {
            if (ImageGroupNavigator == null) return;

            int count = _imageGroup.Items.Count;
            ImageGroupNavigator.Visibility = count > 1 ? Visibility.Visible : Visibility.Collapsed;
            ImageGroupStatusText.Text = count > 0 && SelectedImageIndex >= 0 ? $"{SelectedImageIndex + 1}/{count}" : "0/0";
            ImageGroupPreviousButton.IsEnabled = count > 1 && SelectedImageIndex > 0;
            ImageGroupNextButton.IsEnabled = count > 1 && SelectedImageIndex < count - 1;
        }

        private void ImageGroupPreviousButton_Click(object sender, RoutedEventArgs e)
        {
            SelectImage(SelectedImageIndex - 1);
        }

        private void ImageGroupNextButton_Click(object sender, RoutedEventArgs e)
        {
            SelectImage(SelectedImageIndex + 1);
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
            ApplyImageDocumentMutation(ImageDocumentMutationKind.ImageCleared);
            ClearImageGroup();
            ClearImageEventHandler?.Invoke(this, new EventArgs());
            EditorContext.IImageOpen = null;
            IEditorToolFactory.ApplyImageOpenTools(null);
            SetLayerController(null);
            _session.ClearConfiguration();
            ViewBitmapSource = null;
            ImageShow.Clear();
            Presentation.Publish(null, null);
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
            if (string.IsNullOrWhiteSpace(filePath))
                ClearImageGroup();
            else
                _imageGroup.SetSingle(filePath);

            OpenImageCore(filePath);
        }

        private void OpenImageCore(string? filePath, bool forceReload = false)
        {
            // 普通打开会跳过同一路径；还原原图时强制重新走对应格式的打开器。

            if (filePath == null || (!forceReload && filePath.Equals(Config.GetProperties<string>(ImageViewPropertyKeys.FilePath), StringComparison.Ordinal)))
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

        public bool CanRestoreOriginalImage
        {
            get
            {
                string? filePath = Config.GetProperties<string>(ImageViewPropertyKeys.FilePath);
                return EditorContext.IImageOpen != null && !string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath);
            }
        }

        public bool RestoreOriginalImage()
        {
            if (!Dispatcher.CheckAccess())
            {
                return Dispatcher.Invoke(RestoreOriginalImage);
            }

            string? filePath = Config.GetProperties<string>(ImageViewPropertyKeys.FilePath);
            if (EditorContext.IImageOpen == null || string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            OpenImageCore(filePath, forceReload: true);
            return true;
        }

        public long ImageRevision => _document.Revision;

        public bool IsCurrentImageRevision(long revision)
        {
            return _document.IsCurrent(revision);
        }

        public void NotifySourcePixelsChanged()
        {
            ApplyImageDocumentMutation(ImageDocumentMutationKind.SourcePixelsChanged);
        }

        /// <summary>Commits replacement pixels without reopening the source or resetting view preferences.</summary>
        public void CommitSourcePixels(ImageSource source)
        {
            _session.CommitSourcePixels(source);
        }

        private void ApplyImageDocumentMutation(ImageDocumentMutationKind mutationKind)
        {
            _session.Invalidate(mutationKind);
        }

        public ImageFrameLease? AcquireImageFrame()
        {
            if (Dispatcher.CheckAccess())
            {
                return AcquireImageFrameCore();
            }

            return Dispatcher.Invoke(AcquireImageFrameCore);
        }

        private ImageFrameLease? AcquireImageFrameCore()
        {
            Dispatcher.VerifyAccess();
            return _document.AcquireFrame();
        }


        public void SetImageSource(ImageSource imageSource)
        {
            SetImageSource(imageSource, EnableEditorImageServices, true);
        }


        public void SetImageSource(ImageSource imageSource, bool enableEditorImageServices, bool configureDefaultLayerController)
        {
            if (!_session.TryReplaceSource(imageSource, enableEditorImageServices, () => _isLayerSelectorEnabled = enableEditorImageServices))
            {
                PixelFormat unsupportedFormat = ((WriteableBitmap)imageSource).Format;
                WpfMessageBox.Show(string.Format(Properties.Resources.ImageView_UnsupportedPixelFormat, unsupportedFormat));
                throw new NotSupportedException("The pixel format is not supported.");
            }

            if (configureDefaultLayerController)
            {
                SetLayerController(BitmapImageLayerController.CreateForCurrentImage(this));
            }
            else
            {
                UpdateLayerSelectorVisibility();
            }
            NotifyImageSourceLoaded();
            CommandManager.InvalidateRequerySuggested();

            // 图像加载完成后通知状态栏刷新
            StatusBarItemsChanged?.Invoke(this, EventArgs.Empty);

        }

        public ImageSource FunctionImage
        {
            get => _presentation.FunctionImage!;
            set => _presentation.FunctionImage = value;
        }

        public ImageSource ViewBitmapSource
        {
            get => _document.Source!;
            set => _document.AssignSource(value);
        }

        public void SetLayerController(IImageLayerController? controller)
        {
            if (!ReferenceEquals(_layerController, controller) && _layerController is IDisposable previous)
                previous.Dispose();
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

        public void AddVisual(Visual visual) => _drawing.AddVisual(visual);

        public void ExtractChannel(int channel)
        {
            _channels.SelectChannel(channel);
        }
        public void UpdateZoomAndScale() => _drawing.ZoomToFit();


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
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            ReleaseSnapshotBuffer();
            DebounceTimer.Cancel(_pixelValueOverlayRefreshDebounceKey);
            _realtime?.Dispose();
            Clear();
            _defaultDisplayConfig.PropertyChanged -= DefaultDisplayConfig_PropertyChanged;
            Config.Cleared -= Config_Cleared;
            IEditorToolFactory.Dispose();
            _settingsEntries.Clear();
            EditorContext?.DrawEditorContext.MouseInfoProvider.Dispose();
            EditorContext?.CompactInspectorPresenter?.Dispose();
            EditorContext?.DrawEditorContext.DrawingVisualLists?.Clear();
            _session.Dispose();
            Zoombox1.LayoutUpdated -= Zoombox1_LayoutUpdated;
            Loaded -= ImageView_Loaded;
            Unloaded -= ImageView_Unloaded;
            if (_shortcutWindow != null) _shortcutWindow.PreviewKeyDown -= ShortcutWindow_PreviewKeyDown;
            PreviewKeyDown -= ImageView_PreviewKeyDown;
            ImageShow.PreviewKeyDown -= HandleKeyDown;
            Zoombox1.ContextMenuOpening -= _contextMenus.HandleOpening;
            _drawing.Dispose();
            ComboBoxLayers.SelectionChanged -= ComboBoxLayers_SelectionChanged;

            ImageShow.Dispose();
            Drop -= ImageView_Drop;

            Zoombox1.Child = null;
            ZoomGrid.Children.Clear();
            GC.SuppressFinalize(this);
        }

    }
}
