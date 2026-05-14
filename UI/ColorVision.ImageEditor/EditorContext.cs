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
            : this(imageView, drawCanvas, zoombox, null)
        {
        }

        public EditorContext(ImageView imageView, DrawCanvas drawCanvas, Zoombox zoombox, Panel? textEditorOverlay)
        {
            ImageView = imageView;
            DrawEditorContext = new DrawEditorContext(drawCanvas, zoombox, textEditorOverlay, Id);
        }
        public EditorContext()
        {
            DrawEditorContext = new DrawEditorContext(Id);
        }

        public Guid Id { get; init; } = Guid.NewGuid();
        public ContextMenu ContextMenu { get; set; } = new ContextMenu();
        public IImageOpen? IImageOpen { get; set; }

        public DrawEditorContext DrawEditorContext { get; }

        public ImageView ImageView { get; set; }

        public ObservableCollection<IDrawingVisual> DrawingVisualLists
        {
            get => DrawEditorContext.DrawingVisualLists;
            set => DrawEditorContext.DrawingVisualLists = value;
        }

        public ImageViewConfig Config { get; set; }  = new ImageViewConfig();

        public DrawCanvas DrawCanvas
        {
            get => DrawEditorContext.DrawCanvas;
            set => DrawEditorContext.DrawCanvas = value;
        }

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

        public Zoombox Zoombox
        {
            get => DrawEditorContext.Zoombox;
            set => DrawEditorContext.Zoombox = value;
        }

        public Panel TextEditorOverlay => DrawEditorContext.TextEditorOverlay;

        public Point TranslatePointToTextEditorOverlay(Point point)
        {
            return DrawEditorContext.TranslatePointToTextEditorOverlay(point);
        }

        public double ZoomRatio => DrawEditorContext.ZoomRatio;

        public DrawEditorManager DrawEditorManager => DrawEditorContext.DrawEditorManager;

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
