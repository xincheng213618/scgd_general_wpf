using ColorVision.Common.MVVM;
using log4net;
using log4net.Core;
using log4net.Repository.Hierarchy;
using Newtonsoft.Json;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.UI
{

    public static class TypeLevelCacheHelper
    {
        // 类型全局缓存
        private static readonly Dictionary<Type, List<object>> _typeLevelCache = new();

        /// <summary>
        /// 获取指定类型的所有静态属性和字段（类型为 TLevel），并缓存结果
        /// </summary>
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

    public class LogConfig: ViewModelBase, IConfig, IConfigSettingProvider
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LogConfig));
        public static LogConfig Instance => ConfigService.Instance.GetRequiredService<LogConfig>();

        [JsonIgnore]
        public RelayCommand EditCommand { get; set; }
        public LogConfig()
        {
            EditCommand = new RelayCommand(a => new PropertyEditorWindow(this) {  Owner =Application.Current.GetActiveWindow(), WindowStartupLocation =WindowStartupLocation.CenterOwner }.ShowDialog());
        }
        public static readonly List<string> LogLevels =  GetAllLevels().Select(l => l.Name).ToList();
        public static IReadOnlyList<Level> GetAllLevels() => TypeLevelCacheHelper.GetAllLevels<Level>(typeof(Level));

        private Level _LogLevel = Level.Info;
        public Level LogLevel
        {
            get => _LogLevel; set
            {
                _LogLevel = value;
                NotifyPropertyChanged();
                SetLog();
            }
        }

        public void SetLog()
        {
            var hierarchy = (Hierarchy)LogManager.GetRepository();
            hierarchy.Root.Level = LogLevel;
            log4net.Config.BasicConfigurator.Configure(hierarchy);
        }

        public bool AutoScrollToEnd { get => _AutoScrollToEnd; set { _AutoScrollToEnd = value; NotifyPropertyChanged(); } }
        private bool _AutoScrollToEnd = true;

        public bool AutoRefresh { get => _AutoRefresh; set { _AutoRefresh = value; NotifyPropertyChanged(); } }
        private bool _AutoRefresh;

        public int LogFlushIntervalMs { get => _LogFlushIntervalMs; set { _LogFlushIntervalMs = value; NotifyPropertyChanged(); } }
        private int _LogFlushIntervalMs;


        public LogLoadState LogLoadState { get => _LogLoadState; set { _LogLoadState = value; NotifyPropertyChanged(); } }
        private LogLoadState _LogLoadState = LogLoadState.SinceStartup;

        public bool LogReserve { get => _LogReserve; set { _LogReserve = value; NotifyPropertyChanged(); } }
        private bool _LogReserve;

        public TextWrapping TextWrapping { get => _TextWrapping; set { _TextWrapping = value; NotifyPropertyChanged(); } }
        private TextWrapping _TextWrapping = TextWrapping.NoWrap;

        public int MaxChars { get => _MaxChars; set { _MaxChars = value; NotifyPropertyChanged(); } }
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
