#pragma warning disable CS8625
using ColorVision.UI;
using System.Collections.ObjectModel;

namespace ColorVision.ImageEditor
{
    public class ImageViewComponentManager
    {
        private static ImageViewComponentManager _instance;
        private static readonly object _locker = new();
        public static ImageViewComponentManager GetInstance() { lock (_locker) { return _instance ??= new ImageViewComponentManager(); } }

        public ObservableCollection<IImageViewComponent> IImageViewComponents { get; set; } = new ObservableCollection<IImageViewComponent>();
        public ObservableCollection<IImageViewOpen> IImageViewOpens { get; set; } = new ObservableCollection<IImageViewOpen>();

        public ImageViewComponentManager()
        {
            IImageViewComponents.LoadImplementations();
            IImageViewOpens.LoadImplementations();
        }
    }
}
