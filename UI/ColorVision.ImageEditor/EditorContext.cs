#pragma warning disable CA1859
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Special;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor
{
    public class EditorContext
    {
        private readonly Panel _textEditorOverlay;
        private TextEditingContext? _textEditingContext;

        public EditorContext(
            ImageView imageView,
            ImageViewConfig config,
            DrawEditorContext drawEditorContext,
            ImageProcessingContext processingContext,
            Panel? textEditorOverlay)
        {
            ImageView = imageView ?? throw new ArgumentNullException(nameof(imageView));
            Config = config ?? throw new ArgumentNullException(nameof(config));
            DrawEditorContext = drawEditorContext ?? throw new ArgumentNullException(nameof(drawEditorContext));
            ProcessingContext = processingContext ?? throw new ArgumentNullException(nameof(processingContext));
            _textEditorOverlay = textEditorOverlay ?? CreateFallbackTextEditorOverlay();
        }

        public Guid Id => DrawEditorContext.Id;

        public ImageView ImageView { get; }

        public ContextMenu ContextMenu { get; set; } = new ContextMenu();

        public IImageOpen? IImageOpen { get; set; }

        public DrawEditorContext DrawEditorContext { get; }

        public ObservableCollection<IDrawingVisual> DrawingVisualLists => DrawEditorContext.DrawingVisualLists;

        public DrawCanvas DrawCanvas => DrawEditorContext.DrawCanvas;

        public ImageMouseInfoProvider MouseInfoProvider => DrawEditorContext.MouseInfoProvider;

        public SelectEditorVisual SelectionVisual
        {
            get => DrawEditorContext.SelectionVisual;
            set => DrawEditorContext.SelectionVisual = value;
        }

        public event EventHandler<bool>? ImageEditModeChanged
        {
            add => DrawEditorContext.ImageEditModeChanged += value;
            remove => DrawEditorContext.ImageEditModeChanged -= value;
        }

        public bool IsImageEditMode
        {
            get => DrawEditorContext.IsImageEditMode;
            set => DrawEditorContext.IsImageEditMode = value;
        }

        public Zoombox Zoombox => DrawEditorContext.Zoombox;

        public double ZoomRatio => DrawEditorContext.ZoomRatio;

        public DrawEditorManager DrawEditorManager => DrawEditorContext.DrawEditorManager;

        public Panel TextEditorOverlay => _textEditorOverlay;

        public Point TranslatePointToTextEditorOverlay(Point point)
        {
            return TextEditingContext.TranslatePointToTextEditorOverlay(point);
        }

        public TextEditingContext TextEditingContext => _textEditingContext ??= new TextEditingContext(
            Id,
            DrawEditorContext.DrawCanvas,
            DrawEditorContext.Zoombox,
            _textEditorOverlay,
            DrawEditorContext.SelectionVisual,
            DrawEditorContext.DrawEditorManager,
            DrawEditorContext.DrawingVisualLists);

        public ImageProcessingContext ProcessingContext { get; }

        public ImageViewConfig Config { get; }

        public IEditorToolFactory IEditorToolFactory { get; set; }

        public CompactInspectorPresenter CompactInspectorPresenter { get; set; }

        public Window? OwnerWindow => Window.GetWindow(ImageView) ?? Application.Current?.MainWindow;

        public ImageSource FunctionImage
        {
            get => ImageView.FunctionImage;
            [param: AllowNull]
            set => ImageView.FunctionImage = value;
        }

        public ImageSource ViewBitmapSource
        {
            get => ImageView.ViewBitmapSource;
            [param: AllowNull]
            set => ImageView.ViewBitmapSource = value;
        }

        public void OpenImage(string? filePath)
        {
            ImageView.OpenImage(filePath);
        }

        public void OpenImage(WriteableBitmap? writeableBitmap)
        {
            ImageView.OpenImage(writeableBitmap);
        }

        public void SetImageSource(ImageSource imageSource)
        {
            ImageView.SetImageSource(imageSource);
        }

        public void SaveAs()
        {
            ImageView.SaveAs();
        }

        public void Save(string fileName)
        {
            ImageView.Save(fileName);
        }

        public void Clear()
        {
            ImageView.Clear();
        }

        public void ClearAnnotations()
        {
            ImageView.ClearAnnotations();
        }

        public void ExportAnnotations()
        {
            ImageView.ExportAnnotations();
        }

        public void ImportAnnotations()
        {
            ImageView.ImportAnnotations();
        }

        public void OpenSettingsWindow(string? initialGroup = null)
        {
            ImageView.OpenSettingsWindow(initialGroup);
        }

        internal void SetCompactInspectorItems(IEnumerable<FrameworkElement> elements)
        {
            ImageView.SetCompactInspectorItems(elements);
        }

        public void SetImageEditMode(bool value)
        {
            ImageView.ImageEditMode = value;
        }

        public Task<SelectResult> BeginSelectAsync(SelectShapeType shapeType)
        {
            return ImageView.BeginSelectAsync(shapeType);
        }

        private static Panel CreateFallbackTextEditorOverlay()
        {
            return new Canvas
            {
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed,
            };
        }

    }
}
