using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System.Windows;

namespace ColorVision.UI.LogImp
{
    /// <summary>
    /// WindowLogLocal 配置管理类
    /// </summary>
    public class WindowLogLocalConfig : ViewModelBase
    {
        /// <summary>
        /// 是否自动滚动到日志末尾
        /// </summary>
        public bool AutoScrollToEnd { get => _AutoScrollToEnd; set { _AutoScrollToEnd = value; OnPropertyChanged(); } }
        private bool _AutoScrollToEnd = true;

        /// <summary>
        /// 是否自动刷新日志显示
        /// </summary>
        public bool AutoRefresh { get => _AutoRefresh; set { _AutoRefresh = value; OnPropertyChanged(); } }
        private bool _AutoRefresh = true;

        /// <summary>
        /// 刷新间隔，单位：毫秒
        /// </summary>
        public int RefreshIntervalMs { get => _RefreshIntervalMs; set { _RefreshIntervalMs = value; OnPropertyChanged(); } }
        private int _RefreshIntervalMs = 500;

        /// <summary>
        /// 最大读取行数限制，-1 表示无限制
        /// </summary>
        public int MaxLines { get => _MaxLines; set { _MaxLines = value; OnPropertyChanged(); } }
        private int _MaxLines = 1000;

        /// <summary>
        /// 文本换行模式
        /// </summary>
        public TextWrapping TextWrapping { get => _TextWrapping; set { _TextWrapping = value; OnPropertyChanged(); } }
        private TextWrapping _TextWrapping = TextWrapping.NoWrap;

        [JsonIgnore]
        public RelayCommand EditCommand { get; set; }

        public WindowLogLocalConfig()
        {
            EditCommand = new RelayCommand(a => new PropertyEditorWindow(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
        }
    }
}
