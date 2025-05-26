#pragma warning disable CS8625
using ColorVision.UI;
using System.Collections.ObjectModel;

namespace ColorVision.ImageEditor
{
    public class ComponentManager
    {
        private static ComponentManager _instance;
        private static readonly object _locker = new();
        public static ComponentManager GetInstance() { lock (_locker) { return _instance ??= new ComponentManager(); } }

        public ObservableCollection<IImageComponent> IImageComponents { get; set; } = new ObservableCollection<IImageComponent>();
        public ObservableCollection<IImageOpen> IImageOpens { get; set; } = new ObservableCollection<IImageOpen>();

        public ComponentManager()
        {
            foreach (var item in AssemblyHandler.LoadImplementations<IImageComponent>())
            {
                IImageComponents.Add(item);
            }
            foreach (var item in AssemblyHandler.LoadImplementations<IImageOpen>())
            {
                IImageOpens.Add(item);
            }
        }
    }
}
