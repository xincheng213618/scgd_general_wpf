using ColorVision.Common.MVVM;
using log4net;
using log4net.Core;
using log4net.Repository.Hierarchy;

namespace ColorVision.Settings
{
    public partial class SoftwareSetting
    {
        private string _LogLevel = GlobalConst.LogLevel[0];
        public string LogLevel
        {
            get => _LogLevel; set
            {
                _LogLevel = value;
                NotifyPropertyChanged();
                Level level = Level.All;
                switch (LogLevel)
                {
                    case "info":
                        level = Level.Info;
                        break;
                    case "debug":
                        level = Level.Debug;
                        break;
                    case "warn":
                        level = Level.Warn;
                        break;
                    case "error":
                        level = Level.Error;
                        break;
                    default:
                        level = Level.All;
                        break;
                }

                var hierarchy = (Hierarchy)LogManager.GetRepository();
                hierarchy.Root.Level = level;
                log4net.Config.BasicConfigurator.Configure(hierarchy);
                log.Info("更新Log4Net 日志级别：" + value);
            }
        }
    }

    public partial class SoftwareSetting :ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SoftwareSetting));


        public bool TransparentWindow { get => _TransparentWindow; set { _TransparentWindow = value; NotifyPropertyChanged(); } }
        private bool _TransparentWindow = true;

        /// <summary>
        /// 是否自动更新
        /// </summary>
        public bool IsAutoUpdate { get => _IsAutoUpdate; set { _IsAutoUpdate = value; NotifyPropertyChanged(); } }
        private bool _IsAutoUpdate = true;

        /// <summary>
        /// 是否默认配置
        /// </summary>
        public bool IsDefaultOpenService { get=> _IsDefaultOpenService; set { _IsDefaultOpenService = value;NotifyPropertyChanged(); } }
        private bool _IsDefaultOpenService;

        public bool IsOpenStatusBar { get => _IsOpenStatusBar; set { _IsOpenStatusBar = value; NotifyPropertyChanged(); } }
        private bool _IsOpenStatusBar = true;
        public bool IsOpenSidebar { get => _IsOpenSidebar; set { _IsOpenSidebar = value; NotifyPropertyChanged(); } }
        private bool _IsOpenSidebar = true;

    }
}
