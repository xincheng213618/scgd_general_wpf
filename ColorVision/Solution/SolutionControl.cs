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
    public class SolutionControl
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SolutionControl));

        private static SolutionControl _instance;
        private static readonly object _locker = new();
        public static SolutionControl GetInstance() { lock (_locker) { return _instance ??= new SolutionControl(); } }
        //工程配置文件
        public SolutionConfig SolutionConfig { get => SoftwareConfig.SolutionConfig; }
        public SolutionSetting SolutionSetting { get => SolutionConfig.SolutionSetting; }
        public SoftwareConfig SoftwareConfig { get; private set; }
        public RecentFileList SolutionHistory { get; set; } = new RecentFileList() { Persister = new RegistryPersister("Software\\ColorVision\\SolutionHistory") };

        public SolutionControl()
        {
            SoftwareConfig = GlobalSetting.GetInstance().SoftwareConfig;

            if (string.IsNullOrWhiteSpace(SolutionConfig.SolutionFullName))
                return;
            if (!Directory.Exists(SolutionConfig.SolutionFullName))
            {
                SolutionConfig.SolutionFullName = string.Empty;
                SolutionConfig.SolutionName = string.Empty;
                log.Debug("工程文件失效");
                MessageBox.Show("工程文件不存在，在使用之前请重新创建");
            }
            else
            {
                try
                {
                    if (!Directory.Exists(SolutionConfig.CachePath))
                        Directory.CreateDirectory(SolutionConfig.CachePath);
                }
                catch
                {
                    if (!Directory.Exists(SolutionConfig.CachePath))
                        Tool.CreateDirectory(SolutionConfig.CachePath);
                }
            }


        }

        public bool OpenSolution(string SolutionFullPath)
        {
            if (Directory.Exists(SolutionFullPath))
            {
                DirectoryInfo Info = new DirectoryInfo(SolutionFullPath);
                SolutionConfig.SolutionName = Info.Name;
                SolutionConfig.SolutionFullName = Info.FullName;
                SolutionHistory.InsertFile(Info.FullName);

                try
                {
                    if (!Directory.Exists(SolutionConfig.CachePath))
                        Directory.CreateDirectory(SolutionConfig.CachePath);
                }
                catch
                {
                    if (!Directory.Exists(SolutionConfig.CachePath))
                        Tool.CreateDirectory(SolutionConfig.CachePath);
                }
                return true;
            }
            return false;
        }

        public void CreateSolution(DirectoryInfo SolutionDirectoryInfo)
        {
            SolutionConfig.SolutionName = SolutionDirectoryInfo.Name;
            SolutionConfig.SolutionFullName = SolutionDirectoryInfo.FullName;
            SolutionHistory.InsertFile(SolutionDirectoryInfo.FullName);
            try
            {
                if (!Directory.Exists(SolutionConfig.CachePath))
                    Directory.CreateDirectory(SolutionConfig.CachePath);
            }
            catch
            {
                if (!Directory.Exists(SolutionConfig.CachePath))
                    Tool.CreateDirectory(SolutionConfig.CachePath);
            }

        }

   
    }
}
