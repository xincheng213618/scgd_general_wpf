using ColorVision.Common.MVVM;
using log4net;
using log4net.Core;
using log4net.Repository.Hierarchy;

namespace ColorVision.Settings
{
    public partial class SoftwareSetting :ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SoftwareSetting));

        /// <summary>
        /// 是否默认配置
        /// </summary>
        public bool IsDefaultOpenService { get=> _IsDefaultOpenService; set { _IsDefaultOpenService = value;NotifyPropertyChanged(); } }
        private bool _IsDefaultOpenService;

        public bool IsOpenStatusBar { get => _IsOpenStatusBar; set { _IsOpenStatusBar = value; NotifyPropertyChanged(); } }
        private bool _IsOpenStatusBar = true;
        public bool IsOpenSidebar { get => _IsOpenSidebar; set { _IsOpenSidebar = value; NotifyPropertyChanged(); } }
        private bool _IsOpenSidebar = true;

        private string _LogLevel = GlobalConst.LogLevel[0];
        public string LogLevel
        {
            get => _LogLevel; set
            {
                _LogLevel = value;
                NotifyPropertyChanged();
                Level level = Level.All;
                level = LogLevel switch
                {
                    "info" => Level.Info,
                    "debug" => Level.Debug,
                    "warn" => Level.Warn,
                    "error" => Level.Error,
                    _ => Level.All,
                };

                var hierarchy = (Hierarchy)LogManager.GetRepository();
                hierarchy.Root.Level = level;
                log4net.Config.BasicConfigurator.Configure(hierarchy);
                log.Info("更新Log4Net 日志级别：" + value);
            }
        }

    }
}
