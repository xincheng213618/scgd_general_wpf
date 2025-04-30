using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.Menus;
using log4net;
using Microsoft.Win32;
using OpenTK.Core.Exceptions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.MySql
{

    public class MysqlBack : ViewModelBase
    {
        public ContextMenu ContextMenu { get; set; }

        public RelayCommand RestoreCommand { get; set; }
        public RelayCommand SelectCommand { get; set; }

        public MysqlBack(string filePath)
        {
            FilePath = filePath;
            Name = Path.GetFileName(filePath);
            CreationTime = File.GetCreationTime(filePath);
            RestoreCommand = new RelayCommand(a => Restore());
            SelectCommand = new RelayCommand(a => Select());


            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = "复制", Command = ApplicationCommands.Copy });
            ContextMenu.Items.Add(new MenuItem() { Header = "删除", Command = ApplicationCommands.Delete });
            ContextMenu.Items.Add(new MenuItem() { Header = "还原", Command = RestoreCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = "选中", Command = SelectCommand });

        }

        public void Select()
        {
            PlatformHelper.OpenFolderAndSelectFile(FilePath);
        }

        public void Restore()
        {
            Task.Run(() =>
            {
                MySqlLocalServicesManager.GetInstance().RestoreMysql(FilePath);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), "还原成功,资源文件加载需要重启服务，当前软件也需要重启加载");
                });
            });
        }

        public string FilePath { get => _FilePath; set { _FilePath = value; NotifyPropertyChanged(); } }
        private string _FilePath;
        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;

        public DateTime CreationTime { get => _CreationTime; set { _CreationTime = value; NotifyPropertyChanged(); } }
        private DateTime _CreationTime;




    }

    public class MySqlLocalServicesManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MySqlLocalServicesManager));
        private static MySqlLocalServicesManager _instance;
        private static readonly object _locker = new();
        public static MySqlLocalServicesManager GetInstance() { lock (_locker) { return _instance ??= new MySqlLocalServicesManager(); } }

        public RelayCommand RestoreSelectCommand { get; set; }
        public RelayCommand BackupDatabaseCommand { get; set; }
        public RelayCommand BackupResourcesCommand { get; set; }
        public RelayCommand BackupAllResourcesCommand { get; set; }

        public MySqlLocalServicesManager()
        {
            try
            {
                bool result = FindMySQLPath("MySQL") || FindMySQLPath("MySQL57") || FindMySQLPath("MySQL80");
                if (!result)
                {
                    MessageBox.Show("找不到本地的Mysql服务");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            if (!Directory.Exists(BackupPath))
                Directory.CreateDirectory(BackupPath);
            foreach (var item in Directory.GetFiles(BackupPath))
            {
                if (item.EndsWith(".sql"))
                {
                    Backups.Add(new MysqlBack(item));
                }
            }
            RestoreSelectCommand = new RelayCommand(a => RestoreSelect());
            BackupDatabaseCommand = new RelayCommand(a => BackupDatabase());
            BackupResourcesCommand = new RelayCommand(a => BackupResources());
            BackupAllResourcesCommand = new RelayCommand(a => BackupAllMysql());

        }

        private bool IsRun { get; set; }
        private void BackupDatabase()
        {
            if (IsRun)
            {
                MessageBox.Show("正在执行备份程序");
                return;
            }
            Task.Run(() =>
            {
                IsRun = true;
                BackupMysql();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), "备份成功");
                });
                IsRun = false;
            });
        }

        private void BackupResources()
        {
            if (IsRun)
            {
                MessageBox.Show("正在执行备份程序");
                return;
            }
            Task.Run(() =>
            {
                IsRun = true;
                MySqlLocalServicesManager.GetInstance().BackupMysqlResource();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), "备份成功");
                });
                IsRun = false;
            });
        }


        public void RestoreSelect()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                InitialDirectory = BackupPath, // Set the initial directory
                Filter = "SQL Files (*.sql)|*.sql|All Files (*.*)|*.*", // Filter for file types
                Title = "Select a Backup File"
            };

            // Show the dialog and get the result
            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName; // Get the selected file path

                Task.Run(() =>
                {
                    RestoreMysql(filePath); // Use the selected file path
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(Application.Current.MainWindow, "还原成功,正在重启服务");


                        if (Tool.ExecuteCommandAsAdmin("net stop RegistrationCenterService&&net start RegistrationCenterService"))
                        {
                            MessageBox.Show(Application.Current.MainWindow, "服务重启成功，重启软件");
                            Process.Start(Application.ResourceAssembly.Location.Replace(".dll", ".exe"), "-r");
                            Application.Current.Shutdown();
                        }
                        else
                        {
                            MessageBox.Show(Application.Current.MainWindow, "服务重启失败");
                        }


                    });
                });
            }
        }

        public string BackupPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ColorVision", "Backup");

        public ObservableCollection<MysqlBack> Backups { get; set; } = new ObservableCollection<MysqlBack>();



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

            string BackUpSql = Path.Combine(BackupPath, $"{DateTime.Now:yyyyMMddHHmmss}.sql");
            string backCommnad = $"{MysqldumpPath} -u {MySqlSetting.Instance.MySqlConfig.UserName} -h {MySqlSetting.Instance.MySqlConfig.Host} -p{MySqlSetting.Instance.MySqlConfig.UserPwd} {MySqlSetting.Instance.MySqlConfig.Database} {BackTable} >{BackUpSql}";
            Common.Utilities.Tool.ExecuteCommandUI(backCommnad);
            Application.Current.Dispatcher.Invoke(() =>
            {
                Backups.Add(new MysqlBack(BackUpSql));
            });
        }
        public void BackupAllMysql()
        {
            //备份的信息里应该只包含基础的信息不应该包含许多逻辑
            string BackTable = string.Join(" ", MySqlControl.GetInstance().GetTableNames());

            string BackUpSql = Path.Combine(BackupPath, $"All_{DateTime.Now:yyyyMMddHHmmss}.sql");
            string backCommnad = $"{MysqldumpPath} -u {MySqlSetting.Instance.MySqlConfig.UserName} -h {MySqlSetting.Instance.MySqlConfig.Host} -p{MySqlSetting.Instance.MySqlConfig.UserPwd} {MySqlSetting.Instance.MySqlConfig.Database} {BackTable} >{BackUpSql}";
            Common.Utilities.Tool.ExecuteCommandUI(backCommnad);
            Application.Current.Dispatcher.Invoke(() =>
            {
                Backups.Add(new MysqlBack(BackUpSql));
            });
        }

        public void BackupMysqlResource()
        {
            //备份的信息里应该只包含基础的信息不应该包含许多逻辑
            string BackTable = string.Join(" ", MySqlControl.GetInstance().GetFilteredTableNames());
            string BackUpSql = Path.Combine(BackupPath, $"Res_{DateTime.Now:yyyyMMddHHmmss}.sql");
            string backCommnad = $"{MysqldumpPath} -u {MySqlSetting.Instance.MySqlConfig.UserName} -h {MySqlSetting.Instance.MySqlConfig.Host} -p{MySqlSetting.Instance.MySqlConfig.UserPwd} {MySqlSetting.Instance.MySqlConfig.Database} {BackTable} >{BackUpSql}";
            Common.Utilities.Tool.ExecuteCommandUI(backCommnad);
            Application.Current.Dispatcher.Invoke(() =>
            {
                Backups.Add(new MysqlBack(BackUpSql));
            });
        }



        public void RestoreMysql(string backupFile)
        {
            if (!File.Exists(backupFile))
            {
                MessageBox.Show("Backup file not found.");
                return;
            }
            string restoreCommand = $"{MysqlPath} -u {MySqlSetting.Instance.MySqlConfig.UserName} -h {MySqlSetting.Instance.MySqlConfig.Host} -p{MySqlSetting.Instance.MySqlConfig.UserPwd} {MySqlSetting.Instance.MySqlConfig.Database} < {backupFile}";
            Common.Utilities.Tool.ExecuteCommandUI(restoreCommand);
        }
    }
}
