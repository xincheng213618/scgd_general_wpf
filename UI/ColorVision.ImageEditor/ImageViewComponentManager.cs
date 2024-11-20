#pragma warning disable CS8625
using ColorVision.UI;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.ImageEditor
{
    public class ImageViewComponentManager
    {
        private static ImageViewComponentManager _instance;
        private static readonly object _locker = new();
        public static ImageViewComponentManager GetInstance() { lock (_locker) { return _instance ??= new ImageViewComponentManager(); } }

        public ObservableCollection<IImageViewComponent> IImageViewComponents { get; set; }
        public ObservableCollection<IImageViewOpen> IImageViewOpens { get; set; }

        public ImageViewComponentManager()
        {
            IImageViewComponents = new ObservableCollection<IImageViewComponent>();
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(IImageViewComponent).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is IImageViewComponent componentInitialize)
                    {
                        IImageViewComponents.Add(componentInitialize);
                    }
                }
            }
            IImageViewOpens = new ObservableCollection<IImageViewOpen>();
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(IImageViewOpen).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    if (Activator.CreateInstance(type) is IImageViewOpen imageViewOpen)
                    {
                        IImageViewOpens.Add(imageViewOpen);
                    }
                }
            }

        }
    }
}
