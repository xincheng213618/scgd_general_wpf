#pragma warning disable CS8625
using ColorVision.UI;
using System.Collections.ObjectModel;

namespace ColorVision.ImageEditor
{
    public class ImageComponentManager
    {
        private static ImageComponentManager _instance;
        private static readonly object _locker = new();
        public static ImageComponentManager GetInstance() { lock (_locker) { return _instance ??= new ImageComponentManager(); } }

        public ObservableCollection<IImageComponent> IImageComponents { get; set; } = new ObservableCollection<IImageComponent>();
        public ObservableCollection<IImageOpen> IImageViewOpens { get; set; } = new ObservableCollection<IImageOpen>();

        public ImageComponentManager()
        {
            IImageComponents.LoadImplementations();
            IImageViewOpens.LoadImplementations();
        }
    }
}
