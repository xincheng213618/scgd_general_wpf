using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Special;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.ImageEditor
{
    public class EditorContext
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public EditorContext(ImageView imageView, DrawCanvas drawCanvas, Zoombox zoombox)
        {
            ImageView = imageView;
            DrawCanvas = drawCanvas;
            Zoombox = zoombox;
            MouseInfoProvider = new ImageMouseInfoProvider(this);
        }

        public Guid Id { get; init; } = Guid.NewGuid();
        public ContextMenu ContextMenu { get; set; } = new ContextMenu();
        public IImageOpen? IImageOpen { get; set; }

        public ImageView ImageView { get; set; }

        public ObservableCollection<IDrawingVisual> DrawingVisualLists { get; set; } = new ObservableCollection<IDrawingVisual>();

        public ImageViewConfig Config { get; set; }  = new ImageViewConfig();

        public DrawCanvas DrawCanvas { get; set; }

        public ImageMouseInfoProvider MouseInfoProvider { get; }

        public SelectEditorVisual SelectionVisual { get; set; }

        public event EventHandler<bool>? ImageEditModeChanged;

        public bool IsImageEditMode
        {
            get => _isImageEditMode;
            set
            {
                if (_isImageEditMode == value)
                {
                    return;
                }

                _isImageEditMode = value;
                ImageEditModeChanged?.Invoke(this, value);
            }
        }
        private bool _isImageEditMode;

        public Zoombox Zoombox { get; set; }

        public Panel TextEditorOverlay => ImageView.TextEditorOverlay;

        public Point TranslatePointToTextEditorOverlay(Point point)
        {
            return DrawCanvas.TranslatePoint(point, TextEditorOverlay);
        }

        public double ZoomRatio => Zoombox.ContentMatrix.M11;

        public DrawEditorManager DrawEditorManager { get; init; } = new DrawEditorManager();

        public IEditorToolFactory IEditorToolFactory { get; set; }

        public CompactInspectorPresenter CompactInspectorPresenter { get; set; }

        public void RegisterService<TService>(TService service) where TService : class
        {
            ArgumentNullException.ThrowIfNull(service);
            _services[typeof(TService)] = service;
        }

        public bool TryGetService<TService>(out TService? service) where TService : class
        {
            if (_services.TryGetValue(typeof(TService), out var registeredService) && registeredService is TService typedService)
            {
                service = typedService;
                return true;
            }

            service = null;
            return false;
        }

        public TService GetRequiredService<TService>() where TService : class
        {
            if (TryGetService<TService>(out var service) && service != null)
            {
                return service;
            }
            return null;
        }

        public bool UnregisterService<TService>() where TService : class
        {
            return _services.Remove(typeof(TService));
        }

    }

}
