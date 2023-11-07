using ColorVision.SettingUp;
using System.Diagnostics;
using System;
using System.IO;
using ColorVision.Util;
using ColorVision.RecentFile;
using ColorVision.MySql;
using log4net;
using System.Windows;

namespace ColorVision.Solution
{
    public class CVSolution
    {
        public string ConfigPath { get; set; } = "Config";

        public string CachePath { get; set; } = "Cache";

        public string ImagePath { get; set; } = "Image";
    }



    public delegate int SolutionOpenHandler(string FileName);


    /// <summary>
    /// 工程模块控制中心
    /// </summary>
    public class SolutionManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SolutionManager));

        private static SolutionManager _instance;
        private static readonly object _locker = new();
        public static SolutionManager GetInstance() { lock (_locker) { return _instance ??= new SolutionManager(); } }

        public event SolutionOpenHandler SolutionOpenEvent;

        //工程配置文件
        public SolutionConfig Config { get => SoftwareConfig.SolutionConfig; }
        public SolutionSetting Setting { get => Config.SolutionSetting; }
        public SoftwareConfig SoftwareConfig { get; private set; }
        public RecentFileList SolutionHistory { get; set; } = new RecentFileList() { Persister = new RegistryPersister("Software\\ColorVision\\SolutionHistory") };


        public SolutionManager()
        {
            SoftwareConfig = GlobalSetting.GetInstance().SoftwareConfig;

            if (string.IsNullOrWhiteSpace(Config.SolutionFullName))
                return;
            if (!Directory.Exists(Config.SolutionFullName))
            {
                Config.SolutionFullName = string.Empty;
                Config.SolutionName = string.Empty;
                log.Debug("工程文件失效");
                MessageBox.Show("工程文件不存在，在使用之前请重新创建", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
            }
            else
            {
                Tool.CreateDirectoryMax(Config.CachePath);
            }


        }

        public bool OpenSolution(string SolutionFullPath)
        {
            if (Directory.Exists(SolutionFullPath))
            {
                DirectoryInfo Info = new DirectoryInfo(SolutionFullPath);
                Config.SolutionName = Info.Name;
                Config.SolutionFullName = Info.FullName;
                SolutionHistory.InsertFile(Info.FullName);
                return true;
            }
            return false;
        }

        public void CreateSolution(DirectoryInfo SolutionDirectoryInfo)
        {
            Config.SolutionName = SolutionDirectoryInfo.Name;
            Config.SolutionFullName = SolutionDirectoryInfo.FullName;
            SolutionHistory.InsertFile(SolutionDirectoryInfo.FullName);

            Tool.CreateDirectory(Config.CachePath);
        }

   
    }
}
