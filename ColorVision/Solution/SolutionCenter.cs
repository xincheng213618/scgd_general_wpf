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
    /// <summary>
    /// 工程模块控制中心
    /// </summary>
    public class SolutionCenter
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SolutionCenter));

        private static SolutionCenter _instance;
        private static readonly object _locker = new();
        public static SolutionCenter GetInstance() { lock (_locker) { return _instance ??= new SolutionCenter(); } }
        //工程配置文件
        public SolutionConfig Config { get => SoftwareConfig.SolutionConfig; }
        public SolutionSetting Setting { get => Config.SolutionSetting; }
        public SoftwareConfig SoftwareConfig { get; private set; }
        public RecentFileList SolutionHistory { get; set; } = new RecentFileList() { Persister = new RegistryPersister("Software\\ColorVision\\SolutionHistory") };

        public SolutionCenter()
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
                try
                {
                    if (!Directory.Exists(Config.CachePath))
                        Directory.CreateDirectory(Config.CachePath);
                }
                catch
                {
                    if (!Directory.Exists(Config.CachePath))
                        Tool.CreateDirectory(Config.CachePath);
                }
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

                try
                {
                    if (!Directory.Exists(Config.CachePath))
                        Directory.CreateDirectory(Config.CachePath);
                }
                catch
                {
                    if (!Directory.Exists(Config.CachePath))
                        Tool.CreateDirectory(Config.CachePath);
                }
                return true;
            }
            return false;
        }

        public void CreateSolution(DirectoryInfo SolutionDirectoryInfo)
        {
            Config.SolutionName = SolutionDirectoryInfo.Name;
            Config.SolutionFullName = SolutionDirectoryInfo.FullName;
            SolutionHistory.InsertFile(SolutionDirectoryInfo.FullName);
            try
            {
                if (!Directory.Exists(Config.CachePath))
                    Directory.CreateDirectory(Config.CachePath);
            }
            catch
            {
                if (!Directory.Exists(Config.CachePath))
                    Tool.CreateDirectory(Config.CachePath);
            }

        }

   
    }
}
