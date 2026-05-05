#pragma warning disable CS8625
using ColorVision.Common.Utilities;
using ColorVision.Core;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Settings;
using ColorVision.ImageEditor.Draw.Annotations;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Ruler;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.UI;
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
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.ImageEditor
{
    /// <summary>
    /// ImageShow.xaml 的交互逻辑
    /// </summary>
    public partial class ImageView : UserControl, IDisposable, IActiveDocumentStatusProvider
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ImageView));
        public ImageViewModel ImageViewModel { get; set; }
        public ImageViewConfig Config => ImageViewModel.EditorContext.Config;
        public IPseudoColorService PseudoColorService => EditorContext.GetRequiredService<IPseudoColorService>();

        public ObservableCollection<IDrawingVisual> DrawingVisualLists => ImageViewModel.EditorContext.DrawingVisualLists;

        public event EventHandler ClearImageEventHandler;
        public event EventHandler StatusBarItemsChanged;

        public EditorContext EditorContext { get; set; }

        private readonly List<IImageViewSettingProvider> _imageViewSettingProviders = new();


        public ImageView()
        {
            InitializeComponent();
        }


        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ImageViewModel = new ImageViewModel(this);
            EditorContext = ImageViewModel.EditorContext;
            DataContext = ImageViewModel;
            this.Focusable = true;
            this.Focus();

            Config.Cleared += Config_Cleared;
            InitializeImageViewSettingProviders();

            foreach (var item in ImageViewModel.IEditorToolFactory.IImageComponents)
                item.Execute(this);

            ImageShow.VisualsAdd += ImageShow_VisualsAdd;
            ImageShow.VisualsRemove += ImageShow_VisualsRemove;
            Drop += ImageView_Drop;
            Config.ShowTextChanged += (s, e) =>
            {
                foreach (var drawingVisual in DrawingVisualLists)
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
            Zoombox1.ContentMatrixChanged += (s, e) => UpdateDrawingVisualScale();
            Zoombox1.LayoutUpdated +=(s,e) => UpdateDrawingVisualScale();
            ImageShow.IsLayoutUpdated = Config.IsLayoutUpdated;
            ImageShow.TextFontSizeOverride = Config.DrawingTextFontSize;
            UpdateDrawingVisualScale();
            SetSelectionPropertyPanelVisibility(false);

            Config.ShowMsgChanged += (s, e) =>
            {
                if (!e)
                {
                    foreach (var drawingVisual in DrawingVisualLists)
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

            var _visibilityConfig = ConfigService.Instance.GetRequiredService<EditorToolVisibilityConfig>();

            // Initialize editor tools visibility list
            var EditorTools = new ObservableCollection<EditorToolViewModel>();
            foreach (var tool in ImageViewModel.IEditorToolFactory.IEditorTools.OrderBy(t => t.ToolBarLocal).ThenBy(t => t.Order))
            {
                var guidId = tool.GuidId ?? tool.GetType().Name;
                var toolViewModel = new EditorToolViewModel(this, tool, _visibilityConfig)
                {
                    DisplayName = guidId,
                    Location = tool.ToolBarLocal.ToString(),
                    IsVisible = _visibilityConfig.GetToolVisibility(guidId)
                };
                EditorTools.Add(toolViewModel);
            }
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
                Owner = Window.GetWindow(this) ?? Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            window.ShowDialog();
        }

        internal void SetSelectionPropertyPanelVisibility(bool isVisible)
        {
            SelectionPropertyOverlay.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
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
            RenderTargetBitmap renderTargetBitmap = new((int)ImageShow.ActualWidth, (int)ImageShow.ActualHeight, Config.GetProperties<double>("DpiX"), Config.GetProperties<double>("DpiY"), PixelFormats.Pbgra32);
            renderTargetBitmap.Render(ImageShow);

            // 创建一个PngBitmapEncoder对象来保存位图为PNG文件
            PngBitmapEncoder pngEncoder = new();
            pngEncoder.Frames.Add(BitmapFrame.Create(renderTargetBitmap));

            // 将PNG内容保存到文件
            using FileStream fileStream = new(fileName, FileMode.Create);
            pngEncoder.Save(fileStream);
        }

        public void ClearAnnotations()
        {
            foreach (Visual visual in DrawingVisualLists.OfType<Visual>().ToList())
            {
                ImageShow.RemoveVisual(visual);
            }
        }

        public void ExportAnnotations()
        {
            List<DrawingVisualBase> visuals = DrawingVisualLists.OfType<DrawingVisualBase>().ToList();
            if (visuals.Count == 0)
            {
                MessageBox.Show("当前没有可导出的标注。", "导出标注", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            AnnotationDocument document = AnnotationMapper.CreateDocument(visuals);
            if (document.Items.Count == 0)
            {
                MessageBox.Show("当前图元里没有已接入 annotation 的类型。", "导出标注", MessageBoxButton.OK, MessageBoxImage.Information);
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
                ? $"已导出 {document.Items.Count} 个标注，跳过 {skippedCount} 个未接入 annotation 的图元。"
                : $"已导出 {document.Items.Count} 个标注。";
            MessageBox.Show(message, "导出标注", MessageBoxButton.OK, MessageBoxImage.Information);
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

                MessageBox.Show($"已导入 {visuals.Count} 个标注。", "导入标注", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入标注失败: {ex.Message}", "导入标注", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImageShow_VisualsAdd(object? sender, VisualChangedEventArgs e)
        {
            if (e.Visual is IDrawingVisual visual && !DrawingVisualLists.Contains(visual) && sender is Visual visual1)
            {
                DrawingVisualLists.Add(visual);
            }

        }

        private void ImageShow_VisualsRemove(object? sender, VisualChangedEventArgs e)
        {
            if (e.Visual is IDrawingVisual visual)
                DrawingVisualLists.Remove(visual);
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
            Config.ClearProperties();
            FunctionImage = null;
            ViewBitmapSource = null;
            ImageShow.Clear();
            ImageShow.Source = null;
            ImageShow.UpdateLayout();
            ComboBoxLayers.Visibility = Visibility.Collapsed;
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


        private List<SelectionChangedEventHandler> _handlers = new List<SelectionChangedEventHandler>();
        private readonly Dictionary<string, FrameworkElement> _advancedSettingSections = new();

        public void AddOrReplaceAdvancedSettingSection(string key, FrameworkElement section)
        {
            RemoveAdvancedSettingSection(key);
            SelectionPropertyPanel.Children.Add(section);
            _advancedSettingSections[key] = section;
            SetSelectionPropertyPanelVisibility(SelectionPropertyPanel.Children.Count > 0);
        }

        public void RemoveAdvancedSettingSection(string key)
        {
            if (_advancedSettingSections.TryGetValue(key, out var section))
            {
                SelectionPropertyPanel.Children.Remove(section);
                _advancedSettingSections.Remove(key);
                SetSelectionPropertyPanelVisibility(SelectionPropertyPanel.Children.Count > 0);
            }
        }

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

        public void OpenImage(string? filePath)
        {
            //如果文件已经打开，不会重复打开

            if (filePath == null || filePath.Equals(Config.GetProperties<string>("FilePath"), StringComparison.Ordinal))
            {
                log.Info("文件路径未改变，跳过打开图像。");
                return;
            }
            Config.ClearProperties();
            Config.AddProperties("FilePath", filePath);
            ClearSelectionChangedHandlers();
            try
            {
                if (filePath != null && File.Exists(filePath))
                {
                    long fileSize = new FileInfo(filePath).Length;
                    Config.AddProperties("FileSize", fileSize);

                    string ext = Path.GetExtension(filePath).ToLower(CultureInfo.CurrentCulture);
                    if (ImageViewModel.IEditorToolFactory.IImageOpens.TryGetValue(ext, out var imageOpen))
                    {
                        EditorContext.IImageOpen = imageOpen;
                        EditorContext.IImageOpen.OpenImage(EditorContext, filePath);
                        return;
                    }
                    else
                    {
                        MessageBox.Show($"不支持的图片格式 {ext}");
                    }
                }
            }
            catch(Exception ex)
            {
                log.Error(ex);
                MessageBox.Show(ex.Message);
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
            InvalidatePseudoColorRender();
            FunctionImage = null;
            ViewBitmapSource = null;
            ImageShow.Source = null;
            if (HImageCache != null)
            {
                HImageCache?.Dispose();
                HImageCache = null;
            }

            ComboBoxLayers.Visibility = Visibility.Visible;
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
                        MessageBox.Show($"{writeableBitmap.Format}暂不支持的格式,请联系开发人员");
                        throw new NotSupportedException("The pixel format is not supported.");
                }

                int stride = cols * channels * (depth / 8);

                Config.AddProperties("PixelFormat", writeableBitmap.Format);
                Config.AddProperties("Cols", cols);
                Config.AddProperties("Rows", rows);
                Config.AddProperties("Channel", channels);
                Config.AddProperties("Depth", depth);
                Config.AddProperties("Stride", stride);
                Config.AddProperties("DpiX", writeableBitmap.DpiX);
                Config.AddProperties("DpiY", writeableBitmap.DpiY);
                PseudoColorService?.ConfigureForImage();

                Config.AddProperties("PixelFormat", writeableBitmap.Format);
            }

            ImageCalibrationService.ApplyToDefault(EditorContext);

            ViewBitmapSource = imageSource;
            ImageShow.Source = ViewBitmapSource;

            ImageShow.RaiseImageInitialized();
            CommandManager.InvalidateRequerySuggested();

            // 图像加载完成后通知状态栏刷新
            StatusBarItemsChanged?.Invoke(this, EventArgs.Empty);

        }

        public ImageSource FunctionImage { get; set; }
        public ImageSource ViewBitmapSource { get; set; }

        public void AddVisual(Visual visual)
        {
            ImageShow.AddVisualCommand(visual);
        }

        private void InvalidatePseudoColorRender()
        {
            PseudoColorService?.Invalidate();
        }


        public List<string> ComboBoxLayerItems { get; set; } = new List<string>() { "Src", "R", "G", "B" };

        public void ComboBoxLayersSelectionChanged(object sender, SelectionChangedEventArgs e)
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
            ImageShow.Sacle = GetDrawingVisualScale();
        }

        private double GetDrawingVisualScale()
        {
            double zoomRatio = EditorContext?.ZoomRatio ?? 1;
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
            var mode = new TransientSelectMode(ImageShow, Zoombox1, ImageViewModel, shapeType);
            return mode.Start();
        }

        #endregion

        public void Dispose()
        {
            Clear();
            ImageViewModel.Dispose();
            Config.Cleared -= Config_Cleared;

            ImageShow.VisualsAdd -= ImageShow_VisualsAdd;
            ImageShow.VisualsRemove -= ImageShow_VisualsRemove;

            ImageShow.Dispose();
            Drop -= ImageView_Drop;

            Zoombox1.Child = null;
            ZoomGrid.Children.Clear();
            GC.Collect();
            GC.SuppressFinalize(this);
        }

    }
}
