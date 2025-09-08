#pragma warning disable CS8625
using ColorVision.UI;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace ColorVision.ImageEditor
{
    public interface IImageEditorContextMenuItemProvider
    {
        IEnumerable<MenuItemMetadata> GetMenuItems();
    }

    public class ComponentManager
    {
        private static ComponentManager _instance;
        private static readonly object _locker = new();
        public static ComponentManager GetInstance() { lock (_locker) { return _instance ??= new ComponentManager(); } }

        public ObservableCollection<IImageComponent> IImageComponents { get; set; } = new ObservableCollection<IImageComponent>();
        public Dictionary<string, IImageOpen> IImageOpens { get; set; } = new Dictionary<string, IImageOpen>();




        public ComponentManager()
        {
            foreach (var item in AssemblyService.Instance.LoadImplementations<IImageComponent>())
            {
                IImageComponents.Add(item);
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(IImageOpen).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                    {
                        var attr = type.GetCustomAttributes(typeof(FileExtensionAttribute), false)
                            .Cast<FileExtensionAttribute>().FirstOrDefault();
                        if (attr != null)
                        {
                            foreach (var ext in attr.Extensions)
                            {
                                var extLower = ext.ToLowerInvariant();

                                if (Activator.CreateInstance(type) is IImageOpen instance)
                                {
                                    IImageOpens.Add(extLower, instance);

                                }

                            }
                        }
                    }
                }
            }
        }
    }
}
