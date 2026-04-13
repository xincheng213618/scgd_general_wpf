using log4net;
using System.Reflection;

#pragma warning disable CS8619, CS8625

namespace ColorVision.UI
{
    public class ConfigSettingManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ConfigSettingManager));
        private static ConfigSettingManager _instance;
        private static readonly object _locker = new();
        public static ConfigSettingManager GetInstance() { lock (_locker) { return _instance ??= new ConfigSettingManager(); } }

        private bool _typeCacheBuilt;
        private readonly List<Type> _providerTypeCache = new();
        private readonly List<Type> _configTypeCache = new();
        private List<ConfigSettingMetadata>? _settingsCache;
        private void EnsureTypeCaches()
        {
            if (_typeCacheBuilt) return;
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = (ex.Types?.Where(t => t != null).Cast<Type>().ToArray()) ?? Array.Empty<Type>();
                }
                catch
                {
                    continue;
                }

                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract) continue;
                    if (typeof(IConfigSettingProvider).IsAssignableFrom(t))
                        _providerTypeCache.Add(t);
                    if (typeof(IConfig).IsAssignableFrom(t))
                        _configTypeCache.Add(t);
                }
            }
            _typeCacheBuilt = true;
        }

        public List<ConfigSettingMetadata> GetAllSettings()
        {
            if (_settingsCache != null) return _settingsCache;

            EnsureTypeCaches();
            var allSettings = new List<ConfigSettingMetadata>();

            // 1. 接口方式：IConfigSettingProvider（复杂场景）
            foreach (var t in _providerTypeCache)
            {
                try
                {
                    if (Activator.CreateInstance(t) is IConfigSettingProvider provider)
                    {
                        allSettings.AddRange(provider.GetConfigSettings());
                    }
                }
                catch (Exception ex)
                {
                    log.Warn($"Create IConfigSettingProvider failed: {t.FullName}: {ex.Message}");
                }
            }

            // 2. 属性标注方式：[ConfigSetting] on IConfig properties（简单场景）
            foreach (var configType in _configTypeCache)
            {
                try
                {
                    object instance = null;
                    foreach (var prop in configType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        var attr = prop.GetCustomAttribute<ConfigSettingAttribute>();
                        if (attr == null) continue;

                        // 延迟获取单例：仅在发现属性标注时才去 ConfigService 取实例
                        if (instance == null)
                        {
                            if (ConfigService.Instance == null)
                            {
                                log.Warn($"[ConfigSetting] skipped: ConfigService not initialized for {configType.Name}");
                                break;
                            }
                            try
                            {
                                instance = ConfigService.Instance.GetRequiredService(configType);
                            }
                            catch
                            {
                                log.Warn($"[ConfigSetting] skipped: GetRequiredService failed for {configType.Name}");
                                break;
                            }
                        }

                        allSettings.Add(new ConfigSettingMetadata
                        {
                            Order = attr.Order,
                            Group = attr.Group,
                            Type = ConfigSettingType.Property,
                            BindingName = prop.Name,
                            Source = instance,
                        });
                    }
                }
                catch (Exception ex)
                {
                    log.Warn($"[ConfigSetting] scan failed for {configType.FullName}: {ex.Message}");
                }
            }

            _settingsCache = allSettings
                .GroupBy(s => s.Type)
                .SelectMany(g => g.OrderBy(s => s.Order))
                .ToList();

            return _settingsCache;
        }

        public void InvalidateCache()
        {
            _settingsCache = default;
        }
    }
}

#pragma warning restore CS8619, CS8625
