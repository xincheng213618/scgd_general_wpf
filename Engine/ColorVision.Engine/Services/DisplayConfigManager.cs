using ColorVision.Common.MVVM;
using ColorVision.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;


namespace ColorVision.Engine.Services
{
    public interface IDisplayConfig
    {

    }
    public class IDisplayConfigBase : ViewModelBase, IDisplayConfig
    {
        public bool IsDisplayOpen { get => _IsDisplayOpen; set { _IsDisplayOpen = value; OnPropertyChanged(); } }
        private bool _IsDisplayOpen = true;
    }
    public class DisplayConfigManager : IConfig
    {
        public static DisplayConfigManager Instance => ConfigService.Instance.GetRequiredService<DisplayConfigManager>();

        private readonly JsonSerializer _serializer = new JsonSerializer { Formatting = Formatting.Indented };

        [JsonIgnore]
        public ConcurrentDictionary<string, IDisplayConfig> Configs { get; } = new ConcurrentDictionary<string, IDisplayConfig>();


        private JObject _savedObject = new JObject();
        private readonly object _syncLock = new object(); // 用于同步 JObject 操作的锁

        public JObject Object
        {
            get
            {
                // 3. 这是一个“重”操作，Getter 中包含副作用（Side-effect）其实是不推荐的设计。
                // 但为了保持原逻辑，这里加锁并优化。
                lock (_syncLock)
                {
                    foreach (var configPair in Configs)
                    {
                        try
                        {
                            // 直接使用字段中的 serializer
                            _savedObject[configPair.Key] = JToken.FromObject(configPair.Value, _serializer);
                        }
                        catch (Exception ex)
                        {
                            // 4. 至少打印日志，不要静默吞掉异常
                            // Log.Error($"Failed to serialize config {configPair.Key}: {ex.Message}");
                        }
                    }
                    return _savedObject;
                }
            }
            set
            {
                lock (_syncLock)
                {
                    _savedObject = value ?? new JObject();
                }
            }
        }

        public T GetDisplayConfig<T>(string key) where T : IDisplayConfig, new()
        {
            // 5. 使用 ConcurrentDictionary 的 GetOrAdd 原子操作，或者双重检查锁定
            // 这里为了处理复杂的 JObject 逻辑，我们手动处理，但比递归更安全

            if (Configs.TryGetValue(key, out IDisplayConfig config))
            {
                return (T)config;
            }

            // 如果没找到，尝试从 JObject 加载或新建
            return (T)Configs.GetOrAdd(key, k =>
            {
                T newConfigInstance;

                // 加锁读取 JObject，防止并发修改冲突
                lock (_syncLock)
                {
                    if (_savedObject.TryGetValue(k, out JToken configToken))
                    {
                        try
                        {
                            newConfigInstance = configToken.ToObject<T>(_serializer);
                        }
                        catch
                        {
                            // 如果转换失败，降级为新建默认对象
                            newConfigInstance = new T();
                        }
                    }
                    else
                    {
                        newConfigInstance = new T();
                    }
                }

                // 这里不需要 if (newConfigInstance is IDisplayConfig)，因为泛型约束 T 已经是了
                return newConfigInstance;
            });
        }
    }
}
