using ColorVision.UI.Menus;
using log4net;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.MySql
{
    public class MySqlLocalServicesManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MySqlLocalServicesManager));
        private static MySqlLocalServicesManager _instance;
        private static readonly object _locker = new();
        public static MySqlLocalServicesManager GetInstance() { lock (_locker) { return _instance ??= new MySqlLocalServicesManager(); } }

        public MySqlLocalServicesManager()
        {
            try
            {
                bool result = FindMySQLPath("MySQL")|| FindMySQLPath("MySQL57") || FindMySQLPath("MySQL80");
                if (!result)
                {
                    MessageBox.Show("找不到本地的Mysql服务");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
        bool FindMySQLPath(string serviceName)
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}"))
            {
                if (key != null)
                {
                    ServiceName = serviceName;
                    object imagePath = key.GetValue("ImagePath");
                    if (imagePath is string str)
                    {
                        ImagePath = str;
                        MysqldPath = ExtractExePath(ImagePath);
                        if (File.Exists(MysqldPath))
                        {
                            DirectoryInfo directory = Directory.GetParent(MysqldPath);

                            string mysqlPath = Path.Combine(directory.FullName, "mysql.exe");
                            if (File.Exists(mysqlPath))
                            {
                                MysqlPath = mysqlPath;
                            }
                            string mysqldumpPath = Path.Combine(directory.FullName, "mysqldump.exe");
                            if (File.Exists(mysqldumpPath))
                            {
                                MysqldumpPath = mysqldumpPath;
                            }
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        string ExtractExePath(string imagePath)
        {
            // 切分字符串并提取路径
            var parts = imagePath.Split(' ');
            foreach (var part in parts)
            {
                if (part.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    return part;
                }
            }
            return null;
        }


        public string ServiceName { get; set; } = "MySql80";

        public string ImagePath { get; set; }
        public string MysqldPath { get; set; }

        public string MysqlPath { get; set; }

        public string MysqldumpPath { get; set; }



        public void BackupMysql()
        {
            //备份的信息里应该只包含基础的信息不应该包含许多逻辑
            string BackTable = string.Join(" ", MySqlControl.GetInstance().GetFilteredTableNames());
            
            string BackUpSql = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Backup.sql");
            string backCommnad = $"{MysqldumpPath} -u {MySqlSetting.Instance.MySqlConfig.UserName} -h {MySqlSetting.Instance.MySqlConfig.Host} -p{MySqlSetting.Instance.MySqlConfig.UserPwd} {MySqlSetting.Instance.MySqlConfig.Database} {BackTable} >{BackUpSql}";

            Common.Utilities.Tool.ExecuteCommand(backCommnad);
        }

        public void BackupMysqlResource()
        {
            //备份的信息里应该只包含基础的信息不应该包含许多逻辑
            string BackTable = string.Join(" ", MySqlControl.GetInstance().GetFilteredTableNames());

            string BackUpSql = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Backup.sql");
            string backCommnad = $"{MysqldumpPath} -u {MySqlSetting.Instance.MySqlConfig.UserName} -h {MySqlSetting.Instance.MySqlConfig.Host} -p{MySqlSetting.Instance.MySqlConfig.UserPwd} {MySqlSetting.Instance.MySqlConfig.Database} {BackTable} >{BackUpSql}";

            Common.Utilities.Tool.ExecuteCommand(backCommnad);
        }



        public void RestoreMysql()
        {
            string backupFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Backup.sql");
            if (!File.Exists(backupFile))
            {
                MessageBox.Show("Backup file not found.");
                return;
            }
            string restoreCommand = $"{MysqlPath} -u {MySqlSetting.Instance.MySqlConfig.UserName} -h {MySqlSetting.Instance.MySqlConfig.Host} -p{MySqlSetting.Instance.MySqlConfig.UserPwd} {MySqlSetting.Instance.MySqlConfig.Database} < {backupFile}";
            Common.Utilities.Tool.ExecuteCommand(restoreCommand);
        }
    }
}
