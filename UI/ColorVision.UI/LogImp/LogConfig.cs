using ColorVision.Common.MVVM;
using ColorVision.UI.LogImp;
using log4net;
using log4net.Core;
using log4net.Repository.Hierarchy;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

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
    public class LogConfig: ViewModelBase, IConfig
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
        [ConfigSetting(Order = 15, Section = ConfigSettingConstants.SectionBasic, Description = "LogLevelDescription")]
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
        /// 日志查看器实现模式，打开窗口时生效
        /// </summary>
        [DisplayName("日志查看器模式")]
        [Description("TextBox 为默认文本视图；Virtualized 为高级彩色虚拟化视图，打开日志窗口时生效。")]
        public LogViewerMode LogViewerMode { get => _LogViewerMode; set { _LogViewerMode = value; OnPropertyChanged(); } }
        private LogViewerMode _LogViewerMode = LogViewerMode.TextBox;

        /// <summary>
        /// 是否按日志级别染色
        /// </summary>
        public bool UseLevelColors { get => _UseLevelColors; set { _UseLevelColors = value; OnPropertyChanged(); } }
        private bool _UseLevelColors = true;

        /// <summary>
        /// WARN 日志前景色
        /// </summary>
        public Brush WarningForeground { get => _WarningForeground; set { _WarningForeground = value; OnPropertyChanged(); } }
        private Brush _WarningForeground = CreateBrush(0xB2, 0x6A, 0x00);

        /// <summary>
        /// ERROR 日志前景色
        /// </summary>
        public Brush ErrorForeground { get => _ErrorForeground; set { _ErrorForeground = value; OnPropertyChanged(); } }
        private Brush _ErrorForeground = CreateBrush(0xD3, 0x2F, 0x2F);

        /// <summary>
        /// FATAL 日志前景色
        /// </summary>
        public Brush FatalForeground { get => _FatalForeground; set { _FatalForeground = value; OnPropertyChanged(); } }
        private Brush _FatalForeground = CreateBrush(0xB0, 0x00, 0x20);

        /// <summary>
        /// DEBUG 日志前景色
        /// </summary>
        public Brush DebugForeground { get => _DebugForeground; set { _DebugForeground = value; OnPropertyChanged(); } }
        private Brush _DebugForeground = CreateBrush(0x6E, 0x77, 0x81);

        /// <summary>
        /// TRACE 日志前景色
        /// </summary>
        public Brush TraceForeground { get => _TraceForeground; set { _TraceForeground = value; OnPropertyChanged(); } }
        private Brush _TraceForeground = CreateBrush(0x6E, 0x77, 0x81);

        /// <summary>
        /// 最大字符数限制，-1 表示无限制
        /// </summary>
        public int MaxChars { get => _MaxChars; set { _MaxChars = value; OnPropertyChanged(); } }
        private int _MaxChars = -1;

        /// <summary>
        /// 最大日志条目数限制，-1 表示无限制
        /// </summary>
        public int MaxEntries { get => _MaxEntries; set { _MaxEntries = value; OnPropertyChanged(); } }
        private int _MaxEntries = LogConstants.DefaultMaxEntries;

        private static SolidColorBrush CreateBrush(byte red, byte green, byte blue)
        {
            var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
            brush.Freeze();
            return brush;
        }

    }


}
