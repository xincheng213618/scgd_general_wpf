#pragma warning disable CA1822
using ColorVision.UI;
using ColorVision.Database.Properties;
using ColorVision.UI.Menus;
using log4net;
using SqlSugar;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Database
{
    public interface IInitTables
    {

    }

    public class ExportMySqlInitTables : MenuItemBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ExportMySqlInitTables));

        public override string OwnerGuid => nameof(ExportMySqlMenuItem);
        public override string GuidId => nameof(ExportMySqlInitTables);
        public override string Header => Resources.MenuMySqlInitTables;
        public override int Order => 2;

        private static Stopwatch _stopwatch;

        public override void Execute()
        {
            Task.Run(() => InitTablesAsync()).ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    log.Error("初始化Mysql表失败", t.Exception);
                    MessageBox.Show(Resources.DB_InitFailed, Resources.DB_Error, MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    log.Info("Mysql表初始化完成");
                    MessageBox.Show(Resources.DB_InitSuccess, Resources.DB_Success, MessageBoxButton.OK, MessageBoxImage.Information);
                }
            });

        }

        public  async Task InitTablesAsync()
        {
            _stopwatch = Stopwatch.StartNew();
            await Task.Delay(0); // 模拟异步操作
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(IInitTables).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    try
                    {
                        log.Info($"正在初始化表：{type.Name}");
                        using var DB = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                        DB.CodeFirst.InitTables(type);
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex);
                    }

                }

            }



            _stopwatch.Stop();
            log.Info($"InitTables：{_stopwatch.Elapsed.TotalSeconds} 秒");
        }
        
    }
}
