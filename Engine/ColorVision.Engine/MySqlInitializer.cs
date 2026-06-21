using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.UI;
using ColorVision.UI.ServiceHost;
using log4net;
using SqlSugar;
using System;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

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
        

        public static Version Version { get; set; } = new Version(4,0 ,5,519);

        public override Task Initialize()
        {
            //SqlConfig sqlConfig = ConfigService.Instance.GetRequiredService<SqlConfig>();
            //if (sqlConfig.Version  < Version)
            //{
            //    sqlConfig.Version = Version;
            //    ConfigService.Instance.SaveConfigs();
            //    log.Info($"SqlConfig 版本更新到 {Version}");
            //    Thread thread = new Thread(() =>
            //    {
            //        using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

            //        foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            //        {
            //            foreach (Type type in assembly.GetTypes().Where(t => typeof(IInitTables).IsAssignableFrom(t) && !t.IsAbstract))
            //            {
            //                try
            //                {
            //                    log.Info($"正在初始化表：{type.Name}");
            //                    Db.CodeFirst.InitTables(type);
            //                }
            //                catch (Exception ex)
            //                {
            //                    log.Error(ex);
            //                }

            //            }

            //        }
            //        log.Info("SqlInitialized");
            //    });
            //    thread.Start();
            //}
            return Task.CompletedTask;
        }
    }


    public class MySqlInitializer : InitializerBase
    {
        private const string DefaultMySqlServiceName = "MySQL";
        private static readonly ILog log = LogManager.GetLogger(typeof(MySqlInitializer));
        public override string Name => nameof(MySqlInitializer);
        public override int Order => 1;

        public override async Task InitializeAsync()
        {
            bool isConnect = await MySqlControl.GetInstance().Connect();
            if (isConnect)
                return;

            if (!IsLocalMySqlHost(MySqlControl.Config.Host))
                return;

            await TryRepairLocalMySqlViaServiceHostAsync();
        }

        private static bool IsLocalMySqlHost(string? host)
        {
            return string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task TryRepairLocalMySqlViaServiceHostAsync()
        {
            string serviceName = ResolveMySqlServiceName();

            try
            {
                if (TryGetServiceStatus(serviceName, out ServiceControllerStatus status))
                {
                    if (status == ServiceControllerStatus.Running)
                    {
                        log.Info($"MySQL服务 {serviceName} 已运行，但数据库连接失败，跳过启动阶段服务修复。");
                        return;
                    }

                    log.Info($"检测MySQL服务 {serviceName}，状态{status}，正在通过ColorVisionServiceHost启动服务。");
                    ServiceHostResponse response = await ColorVisionServiceHostClient.Default.StartServiceAsync(
                        serviceName,
                        timeoutSeconds: 45,
                        timeout: TimeSpan.FromSeconds(60));

                    if (!response.Success)
                    {
                        log.Warn($"ColorVisionServiceHost启动MySQL服务失败：{response.Message}");
                        return;
                    }

                    if (!await MySqlControl.GetInstance().Connect())
                        log.Warn("MySQL服务已启动，但数据库仍无法连接。");
                }
                else
                {
                    await RepairMissingMySqlServiceViaServiceHostAsync(serviceName);
                }
            }
            catch (Exception ex)
            {
                log.Warn("启动阶段通过ColorVisionServiceHost修复MySQL失败，已跳过UAC弹窗。", ex);
            }
        }

        private static string ResolveMySqlServiceName()
        {
            string configuredName = MySqlLocalConfig.Instance.ServiceName?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(configuredName) && TryGetServiceStatus(configuredName, out _))
                return configuredName;

            if (TryGetServiceStatus(DefaultMySqlServiceName, out _))
                return DefaultMySqlServiceName;

            return DefaultMySqlServiceName;
        }

        private static bool TryGetServiceStatus(string serviceName, out ServiceControllerStatus status)
        {
            status = default;
            try
            {
                using ServiceController serviceController = new(serviceName);
                status = serviceController.Status;
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static async Task RepairMissingMySqlServiceViaServiceHostAsync(string serviceName)
        {
            string mysqldPath = MySqlLocalConfig.Instance.MysqldPath;
            if (string.IsNullOrWhiteSpace(mysqldPath) || !File.Exists(mysqldPath))
            {
                log.Warn($"MySQL服务 {serviceName} 未安装，且保存的mysqld.exe路径无效：{mysqldPath}");
                return;
            }

            log.Info($"MySQL服务 {serviceName} 未安装，正在通过ColorVisionServiceHost静默注册并启动服务。");
            ServiceHostResponse response = await ColorVisionServiceHostClient.Default.RepairMySqlServiceAsync(
                serviceName,
                mysqldPath,
                timeoutSeconds: 60,
                timeout: TimeSpan.FromSeconds(90));

            if (!response.Success)
            {
                log.Warn($"ColorVisionServiceHost修复MySQL服务失败：{response.Message}");
                return;
            }

            if (!await MySqlControl.GetInstance().Connect())
                log.Warn("MySQL服务已修复并启动，但数据库仍无法连接。");
        }
    }
}
