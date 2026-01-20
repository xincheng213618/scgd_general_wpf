using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.UI;
using log4net;
using SqlSugar;
using System;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine
{

    public class SqlConfig : ViewModelBase, IConfig
    {
        public Version Version { get => _Version; set { _Version = value; OnPropertyChanged(); } }
        private Version _Version = new Version(0, 0, 0);
    }

    public class SqlInitialized : MainWindowInitializedBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SqlInitialized));
        

        public static Version Version { get; set; } = new Version(4,0 , 1,103);

        public override Task Initialize()
        {
            SqlConfig sqlConfig = ConfigService.Instance.GetRequiredService<SqlConfig>();
            if (sqlConfig.Version  < Version)
            {
                sqlConfig.Version = Version;
                ConfigService.Instance.SaveConfigs();
                log.Info($"SqlConfig 版本更新到 {Version}");
                Thread thread = new Thread(() =>
                {
                    using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

                    foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
                    {
                        foreach (Type type in assembly.GetTypes().Where(t => typeof(IInitTables).IsAssignableFrom(t) && !t.IsAbstract))
                        {
                            try
                            {
                                log.Info($"正在初始化表：{type.Name}");
                                Db.CodeFirst.InitTables(type);
                            }
                            catch (Exception ex)
                            {
                                log.Error(ex);
                            }

                        }

                    }
                    log.Info("SqlInitialized");
                });
                thread.Start();
            }
            return Task.CompletedTask;
        }
    }


    public class MySqlInitializer : InitializerBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MySqlInitializer));
        public override string Name => nameof(MySqlInitializer);
        public override int Order => 1;

        public override  async Task InitializeAsync()
        {
            if (MySqlSetting.Instance.IsUseMySql)
            {
                log.Info("正在检测MySql数据库连接情况");
                bool isConnect = await MySqlControl.GetInstance().Connect();

                log.Info($"MySql数据库连接{(MySqlControl.GetInstance().IsConnect ? Properties.Resources.Success : Properties.Resources.Failure)}");

                if (!isConnect)
                {
                    if (MySqlControl.Config.Host == "127.0.0.1" || MySqlControl.Config.Host == "localhost")
                    {
                        try
                        {
                            ServiceController serviceController = new ServiceController("MySQL");
                            try
                            {
                                var status = serviceController.Status;
                                log.Info($"检测服务，状态{status}，正在尝试启动服务");
                                if (Tool.IsAdministrator())
                                {
                                    serviceController.Start();
                                    isConnect = await MySqlControl.GetInstance().Connect();
                                    if (isConnect) return;
                                }
                                else
                                {
                                    if (Tool.ExecuteCommandAsAdmin("net start MySQL"))
                                    {
                                        isConnect = await MySqlControl.GetInstance().Connect();
                                        if (isConnect) return;
                                    }
                                }
                            }
                            catch (InvalidOperationException)
                            {
                                // 服务不存在
                                if (File.Exists(MySqlLocalConfig.Instance.MysqldPath))
                                {
                                    log.Info("MySQL服务未安装，正在尝试手动安装MySQL服务。");

                                    string cmd = $"{MySqlLocalConfig.Instance.MysqldPath} --install MySQL&&net start MySQL";
                                    if (Tool.ExecuteCommandAsAdmin(cmd))
                                    {
                                        isConnect = await MySqlControl.GetInstance().Connect();
                                        if (isConnect) return;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            log.Error(ex.Message);
                        }
                    }
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MySqlConnect mySqlConnect = new MySqlConnect() { Owner = Application.Current.GetActiveWindow() };
                        mySqlConnect.ShowDialog();
                    });
                }
                

            }
        }
    }
}
