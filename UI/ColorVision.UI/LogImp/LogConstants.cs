namespace ColorVision.UI.LogImp
{
    /// <summary>
    /// 日志系统常量定义
    /// </summary>
    public static class LogConstants
    {
        /// <summary>
        /// UI 响应式布局 - 自动滚动按钮最小宽度
        /// </summary>
        public const int MinWidthForAutoScrollButton = 600;

        /// <summary>
        /// UI 响应式布局 - 自动刷新按钮最小宽度
        /// </summary>
        public const int MinWidthForAutoRefreshButton = 500;

        /// <summary>
        /// UI 响应式布局 - 日志级别下拉框最小宽度
        /// </summary>
        public const int MinWidthForLevelComboBox = 400;

        /// <summary>
        /// UI 响应式布局 - 搜索栏最小宽度
        /// </summary>
        public const int MinWidthForSearchBar = 200;

        /// <summary>
        /// 默认批量刷新间隔，单位：毫秒
        /// </summary>
        public const int DefaultFlushIntervalMs = 100;

        /// <summary>
        /// 默认最大字符数限制
        /// </summary>
        public const int DefaultMaxChars = 100000;

        /// <summary>
        /// 启用字符数截断的最小阈值
        /// </summary>
        public const int MinMaxCharsForTrimming = 1000;

        /// <summary>
        /// 自动滚动恢复延迟，单位：秒
        /// </summary>
        public const int AutoScrollResumeDelaySeconds = 2;

        /// <summary>
        /// 默认日志格式模式
        /// </summary>
        public const string DefaultLogPattern = "%date [%thread] %-5level %logger %  %message%newline";

        /// <summary>
        /// 日志时间戳格式
        /// </summary>
        public const string LogTimestampFormat = "yyyy-MM-dd HH:mm:ss,fff";

        /// <summary>
        /// 日志时间戳长度
        /// </summary>
        public const int LogTimestampLength = 23;
    }
}
