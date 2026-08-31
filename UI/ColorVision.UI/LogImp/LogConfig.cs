using ColorVision.Common.MVVM;
using ColorVision.UI.LogImp;
using log4net;
using log4net.Core;
using log4net.Repository.Hierarchy;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Reflection;

namespace ColorVision.UI
{
    /// <summary>
    /// 类型级别缓存辅助类，用于缓存反射扫描结果
    /// </summary>
    public static class TypeLevelCacheHelper
    {
        // 使用 (sourceType, levelType) 复合键直接缓存强类型只读列表，避免缓存命中时重复分配
        private static readonly Dictionary<(Type, Type), object> _typedLevelCache = new();

        /// <summary>
        /// 获取指定类型的所有静态属性和字段（类型为 TLevel），并缓存结果
        /// </summary>
        /// <typeparam name="TLevel">目标级别类型</typeparam>
        /// <param name="type">要扫描的类型</param>
        /// <returns>类型为 TLevel 的所有静态成员只读列表</returns>
        public static IReadOnlyList<TLevel> GetAllLevels<TLevel>(Type type)
        {
            var key = (type, typeof(TLevel));
            if (_typedLevelCache.TryGetValue(key, out var cached))
            {
                return (IReadOnlyList<TLevel>)cached;
            }

            var levels = new List<TLevel>();

            // 静态属性
            var props = type.GetProperties(BindingFlags.Static | BindingFlags.Public);
            foreach (var p in props)
            {
                if (typeof(TLevel).IsAssignableFrom(p.PropertyType))
                {
                    if (p.GetValue(null) is TLevel value)
                    {
                        levels.Add(value);
                    }
                }
            }

            // 静态字段
            var fields = type.GetFields(BindingFlags.Static | BindingFlags.Public);
            foreach (var f in fields)
            {
                if (typeof(TLevel).IsAssignableFrom(f.FieldType))
                {
                    if (f.GetValue(null) is TLevel value && !levels.Contains(value))
                    {
                        levels.Add(value);
                    }
                }
            }

            // 缓存为只读列表，后续命中直接返回，无需额外分配
            var result = levels.AsReadOnly();
            _typedLevelCache[key] = result;

            return result;
        }
    }

    /// <summary>
    /// 日志配置管理类，管理日志系统的各项配置
    /// </summary>
    public class LogConfig : LogViewConfig, IConfig
    {
        /// <summary>
        /// 获取 LogConfig 单例实例
        /// </summary>
        public static LogConfig Instance => ConfigService.Instance.GetRequiredService<LogConfig>();

        /// <summary>
        /// 所有可用的日志级别名称列表
        /// </summary>
        public static readonly List<string> LogLevels =  GetAllLevels().Select(l => l.Name).ToList();
        
        /// <summary>
        /// 获取所有日志级别
        /// </summary>
        /// <returns>日志级别只读列表</returns>
        public static IReadOnlyList<Level> GetAllLevels() => TypeLevelCacheHelper.GetAllLevels<Level>(typeof(Level));

        private Level _LogLevel = Level.Info;

        /// <summary>
        /// 当前日志级别
        /// </summary>
        [ConfigSetting(Order = 15, Section = ConfigSettingConstants.SectionDiagnostics, Description = "LogLevelDescription")]
        [JsonIgnore]
        [PropertyEditorTypeAttribute(typeof(LevelPropertiesEditor))]
        public Level LogLevel
        {
            get => _LogLevel; set
            {
                _LogLevel = value;
                OnPropertyChanged();
                SetLog();
            }
        }

        /// <summary>
        /// 日志级别字符串表示（用于序列化）
        /// </summary>
        public string LogLevelString { get => LogLevel.ToString();
            set 
            {
                var found = GetAllLevels().FirstOrDefault(l => l.Name == value);
                if (found != null)
                {
                    _LogLevel = found;
                }
                else
                {
                    _LogLevel = Level.Info;
                }
            }
        }

        /// <summary>
        /// 应用日志级别设置到 log4net
        /// </summary>
        public void SetLog()
        {
            var hierarchy = (Hierarchy)LogManager.GetRepository();
            hierarchy.Root.Level = LogLevel;
            log4net.Config.BasicConfigurator.Configure(hierarchy);
        }
    }


}
