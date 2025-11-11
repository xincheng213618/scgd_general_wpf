using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.Collections.Generic;
using Newtonsoft;
using Newtonsoft.Json;


namespace ColorVision.Engine.Services
{
    public interface IDisPlayConfig
    {

    }
    public class IDisPlayConfigBase : ViewModelBase, IDisPlayConfig
    {
        public bool IsDisplayOpen { get => _IsDisplayOpen; set { _IsDisplayOpen = value; OnPropertyChanged(); } }
        private bool _IsDisplayOpen = true;
    }
    public class DisplayConfigManager : IConfig
    {
        public static DisplayConfigManager Instance => ConfigService.Instance.GetRequiredService<DisplayConfigManager>();

        public Dictionary<string, IDisPlayConfig> keyValuePairs { get; set; } = new Dictionary<string, IDisPlayConfig>();

        public T GetDisplayCameraConfig<T>(string key) where T : IDisPlayConfig, new()
        {
            if (keyValuePairs.TryGetValue(key, out IDisPlayConfig config) && config is T t)
            {
                return t;
            }
            else
            {
                T t1 = new T();
                keyValuePairs.Add(key, t1);
                return t1;
            }
        }
    }
}
