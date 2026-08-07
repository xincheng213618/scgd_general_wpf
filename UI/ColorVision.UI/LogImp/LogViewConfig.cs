using ColorVision.Common.MVVM;
using ColorVision.UI.LogImp;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.UI
{
    /// <summary>
    /// 日志视图的局部显示配置，每个日志界面应使用独立实例。
    /// </summary>
    public class LogViewConfig : ViewModelBase
    {
        [JsonIgnore]
        public RelayCommand EditCommand { get; set; }

        public LogViewConfig()
        {
            EditCommand = new RelayCommand(_ => new PropertyEditorWindow(this)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.ShowDialog());
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

    /// <summary>
    /// 实时日志面板配置，默认只保留较小的日志窗口以避免 UI 全量重建。
    /// </summary>
    public class RealtimeLogViewConfig : LogViewConfig
    {
        public RealtimeLogViewConfig()
        {
            AutoRefresh = true;
            MaxEntries = LogConstants.DefaultRealtimeLogMaxEntries;
        }
    }

    /// <summary>
    /// 主界面日志面板的持久化显示配置。
    /// </summary>
    public sealed class LogPanelConfig : RealtimeLogViewConfig, IConfig
    {
        public static LogPanelConfig Instance => ConfigService.Instance.GetRequiredService<LogPanelConfig>();
    }
}
