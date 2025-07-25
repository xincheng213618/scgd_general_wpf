using ColorVision.Common.MVVM;
using log4net;
using log4net.Core;
using log4net.Repository.Hierarchy;
using Newtonsoft.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.UI
{
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

        public static readonly List<string> LogLevels = new() { "all", "debug", "info", "warning", "error", "none" };
        public static IEnumerable<Level> GetAllLevels()
        {
            return new List<Level> { Level.All, Level.Trace, Level.Debug, Level.Info, Level.Warn, Level.Error, Level.Critical, Level.Alert, Level.Fatal, Level.Off };
        }
        private Level _LogLevel = Level.Info;
        public Level LogLevel
        {
            get => _LogLevel; set
            {
                _LogLevel = value;
                NotifyPropertyChanged();
                LogLevelName = value.Name;
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

        public string LogLevelName
        {
            get => LogLevel.Name;
            set
            {
                if (value != LogLevel.Name)
                {
                    LogLevel = GetAllLevels().FirstOrDefault(level => level.Name == value) ?? Level.Info;
                }
            }
        }

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
