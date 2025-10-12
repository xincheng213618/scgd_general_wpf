using ColorVision.Common.MVVM;
using ColorVision.UI.LogImp;
using log4net;
using log4net.Core;
using log4net.Repository.Hierarchy;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.UI
{
    /// <summary>
    /// 类型级别缓存辅助类，用于缓存反射扫描结果
    /// </summary>
    public static class TypeLevelCacheHelper
    {
        // 类型全局缓存
        private static readonly Dictionary<Type, List<object>> _typeLevelCache = new();

        /// <summary>
        /// 获取指定类型的所有静态属性和字段（类型为 TLevel），并缓存结果
        /// </summary>
        /// <typeparam name="TLevel">目标级别类型</typeparam>
        /// <param name="type">要扫描的类型</param>
        /// <returns>类型为 TLevel 的所有静态成员列表</returns>
        public static IReadOnlyList<TLevel> GetAllLevels<TLevel>(Type type)
        {
            if (_typeLevelCache.TryGetValue(type, out var cached))
            {
                return cached.Cast<TLevel>().ToList();
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

            // 缓存结果
            _typeLevelCache[type] = levels.Cast<object>().ToList();

            return levels;
        }
    }

    /// <summary>
    /// 日志配置管理类，管理日志系统的各项配置
    /// </summary>
    public class LogConfig: ViewModelBase, IConfig, IConfigSettingProvider
    {
        /// <summary>
        /// 获取 LogConfig 单例实例
        /// </summary>
        public static LogConfig Instance => ConfigService.Instance.GetRequiredService<LogConfig>();

        [JsonIgnore]
        public RelayCommand EditCommand { get; set; }
        
        public LogConfig()
        {
            EditCommand = new RelayCommand(a => new PropertyEditorWindow(this) {  Owner =Application.Current.GetActiveWindow(), WindowStartupLocation =WindowStartupLocation.CenterOwner }.ShowDialog());
        }
        
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

        /// <summary>
        /// 是否自动滚动到日志末尾
        /// </summary>
        public bool AutoScrollToEnd { get => _AutoScrollToEnd; set { _AutoScrollToEnd = value; OnPropertyChanged(); } }
        private bool _AutoScrollToEnd = true;

        /// <summary>
        /// 是否自动刷新日志显示
        /// </summary>
        public bool AutoRefresh { get => _AutoRefresh; set { _AutoRefresh = value; OnPropertyChanged(); } }
        private bool _AutoRefresh;

        /// <summary>
        /// 日志刷新间隔，单位：毫秒
        /// </summary>
        public int LogFlushIntervalMs { get => _LogFlushIntervalMs; set { _LogFlushIntervalMs = value; OnPropertyChanged(); } }
        private int _LogFlushIntervalMs;

        /// <summary>
        /// 日志加载策略
        /// </summary>
        public LogLoadState LogLoadState { get => _LogLoadState; set { _LogLoadState = value; OnPropertyChanged(); } }
        private LogLoadState _LogLoadState = LogLoadState.SinceStartup;

        /// <summary>
        /// 是否倒序显示日志（最新日志在顶部）
        /// </summary>
        public bool LogReserve { get => _LogReserve; set { _LogReserve = value; OnPropertyChanged(); } }
        private bool _LogReserve;

        /// <summary>
        /// 文本换行模式
        /// </summary>
        public TextWrapping TextWrapping { get => _TextWrapping; set { _TextWrapping = value; OnPropertyChanged(); } }
        private TextWrapping _TextWrapping = TextWrapping.NoWrap;

        /// <summary>
        /// 最大字符数限制，-1 表示无限制
        /// </summary>
        public int MaxChars { get => _MaxChars; set { _MaxChars = value; OnPropertyChanged(); } }
        private int _MaxChars = -1;

        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            TextBox textBox = new TextBox();
            ComboBox cmlog = new ComboBox() { SelectedValuePath = "Key", DisplayMemberPath = "Value" };
            cmlog.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedValueProperty, new Binding(nameof(LogLevel)));

            cmlog.ItemsSource = GetAllLevels().Select(level => new KeyValuePair<Level, string>(level, level.Name));

            cmlog.SelectionChanged += (s, e) => {

            };
            cmlog.DataContext = Instance;


            return new List<ConfigSettingMetadata>
            {
                new ConfigSettingMetadata
                {
                    Name = Properties.Resources.LogLevel,
                    Description =  Properties.Resources.LogLevel,
                    Order = 15,
                    Type = ConfigSettingType.ComboBox,
                    ComboBox = cmlog,
                },
            };
        }

    }


}
