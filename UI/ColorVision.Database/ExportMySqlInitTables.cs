using ColorVision.UI;
using ColorVision.UI.Menus;
using log4net;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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
        public override string Header => "MySqlInitTables(调试)";
        public override int Order => 2;

        private static Stopwatch _stopwatch;

        public override void Execute()
        {
            Task.Run(() => InitTablesAsync()).ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    log.Error("初始化Mysql表失败", t.Exception);
                    MessageBox.Show("初始化Mysql表失败，请查看日志。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    log.Info("Mysql表初始化完成");
                    MessageBox.Show("Mysql表初始化完成。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
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
                        MySqlControl.GetInstance().DB.CodeFirst.InitTables(type);
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
