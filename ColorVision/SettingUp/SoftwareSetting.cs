using ColorVision.MVVM;
using log4net;
using log4net.Core;
using log4net.Repository.Hierarchy;

namespace ColorVision
{
    public class SoftwareSetting :ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SoftwareSetting));

        public SoftwareSetting()
        {
        }

        /// <summary>
        /// 主题
        /// </summary>
        public Themes.Theme Theme { get; set; } = ColorVision.Themes.Theme.Light;
        /// <summary>
        /// 语言
        /// </summary>
        public string UICulture { get; set; } = "zh-Hans";


        public bool IsRestoreWindow { get; set; }

        public double Width { get; set; }
        public double Height { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public int WindowState { get; set; }

        public bool IsDeFaultOpenService { get=> _IsDeFaultOpenService; set { _IsDeFaultOpenService = value;NotifyPropertyChanged(); } }
        private bool _IsDeFaultOpenService = true;

        public bool IsOpenStatusBar { get => _IsOpenStatusBar; set { _IsOpenStatusBar = value; NotifyPropertyChanged(); } }
        private bool _IsOpenStatusBar = true;
        public bool IsOpenSidebar { get => _IsOpenSidebar; set { _IsOpenSidebar = value; NotifyPropertyChanged(); } }
        private bool _IsOpenSidebar = true;


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
                log.Info("更新log4Net" + value);
            }
        }
        private string _LogLevel = GlobalConst.LogLevel[0];
    }
}
