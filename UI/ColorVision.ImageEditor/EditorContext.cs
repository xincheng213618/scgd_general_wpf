using ColorVision.ImageEditor.Abstractions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.ImageEditor
{
    public class EditorContext
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public Guid Id { get; init; } = Guid.NewGuid();
        public ContextMenu ContextMenu { get; set; } = new ContextMenu();
        public IImageOpen? IImageOpen { get; set; }

        public ImageView ImageView { get; set; }

        public ObservableCollection<IDrawingVisual> DrawingVisualLists { get; set; } = new ObservableCollection<IDrawingVisual>();

        public ImageViewModel ImageViewModel { get; set; }

        public ImageViewConfig Config { get; set; }  = new ImageViewConfig();

        public DrawCanvas DrawCanvas { get; set; }

        public Zoombox Zoombox { get; set; }

        public double ZoomRatio => Zoombox.ContentMatrix.M11;

        public DrawEditorManager DrawEditorManager { get; init; } = new DrawEditorManager();

        public IEditorToolFactory IEditorToolFactory { get; set; }

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

            throw new InvalidOperationException($"EditorContext service {typeof(TService).FullName} is not registered.");
        }

        public bool UnregisterService<TService>() where TService : class
        {
            return _services.Remove(typeof(TService));
        }

    }

}
