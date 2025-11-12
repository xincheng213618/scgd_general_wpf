using ColorVision.Common.MVVM;
using ColorVision.UI;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;


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

        internal readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings { Formatting = Formatting.Indented };

        public JObject Object { 
            get 
            {
                foreach (var configPair in Configs)
                {
                    _Object[configPair.Key] = JToken.FromObject(configPair.Value, JsonSerializer.Create(JsonSerializerSettings));
                }
                return _Object;
            }
            set 
            { 
                _Object = value;
            }
        }
        private JObject _Object = new JObject();


        [JsonIgnore]
        public Dictionary<string, IDisPlayConfig> Configs { get; set; } = new Dictionary<string, IDisPlayConfig>();

        public T GetDisplayConfig<T>(string key) where T : IDisPlayConfig, new()
        {
            var type = typeof(T);
            if (Configs.TryGetValue(key, out IDisPlayConfig config))
            {
                return (T)config;
            }
            else
            {
                var configName = typeof(T).Name;

                if (_Object.TryGetValue(key, out JToken configToken))
                {
                    var config1 = configToken.ToObject(type, new JsonSerializer { Formatting = Formatting.Indented });
                    if (config1 is IDisPlayConfig configInstance)
                    {
                        Configs[key] = configInstance;
                    }
                }
                else
                {
                    if (Activator.CreateInstance(type) is IDisPlayConfig defaultConfig)
                    {
                        Configs[key] = defaultConfig;
                    }
                }
                return GetDisplayConfig<T>(key);
            }
        }
    }
}
