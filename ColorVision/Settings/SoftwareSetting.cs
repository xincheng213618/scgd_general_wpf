using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using ColorVision.UI.Configs;
using ColorVision.Update;
using log4net;
using log4net.Core;
using log4net.Repository.Hierarchy;
using System.Collections.Generic;

namespace ColorVision.Settings
{

    public class SoftwareSettingProvider : ViewModelBase, IConfigSettingProvider
    {
        public bool IsAutoRun { get => Tool.IsAutoRun(GlobalConst.AutoRunName, GlobalConst.AutoRunRegPath); set { Tool.SetAutoRun(value, GlobalConst.AutoRunName, GlobalConst.AutoRunRegPath); NotifyPropertyChanged(); } }

        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata>
            {
                new ConfigSettingMetadata
                {
                    Name = Properties.Resource.TbSettingsStartBoot,
                    Description =  Properties.Resource.TbSettingsStartBoot,
                    Order = 15,
                    Type = ConfigSettingType.Bool,
                    BindingName =nameof(IsAutoRun),
                    Source = this,
                }
            };
        }
    }


    public partial class SoftwareSetting :ViewModelBase,IConfig
    {
        public static SoftwareSetting Instance => ConfigHandler.GetInstance().GetRequiredService<SoftwareSetting>();

        private static readonly ILog log = LogManager.GetLogger(typeof(SoftwareSetting));

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
        public string? Version { get => _Version; set { _Version = value; NotifyPropertyChanged(); } }
        private string? _Version = string.Empty;
    }
}
