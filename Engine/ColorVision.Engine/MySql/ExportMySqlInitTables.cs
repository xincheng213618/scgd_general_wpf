using ColorVision.Engine.Services.Dao;
using ColorVision.UI.Menus;
using log4net;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.MySql
{
    public interface IInitTables
    {

    }

    public class ExportMySqlInitTables : MenuItemBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ExportMySqlInitTables));

        public override string OwnerGuid => nameof(ExportMySqlMenuItem);
        public override string GuidId => nameof(ExportMySqlInitTables);
        public override string Header => "MySqlInitTables";
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
            var foundTypes = Assembly.GetAssembly(typeof(MySqlInitializer)).GetTypes()
            .Where(t => typeof(IInitTables).IsAssignableFrom(t) && // 类型 t 实现了接口 T
                        !t.IsInterface &&                         // 类型 t 本身不是接口
                        !t.IsAbstract &&                          // 类型 t 不是抽象类
                        t.GetConstructor(Type.EmptyTypes) != null); // 类型 t 有公共无参构造函数


            //foreach (var item in foundTypes)
            //{
            //    try
            //    {
            //        await Task.Delay(0); // 模拟异步操作
            //        log.Info($"正在初始化表：{item.Name}");
            //        MySqlControl.GetInstance().DB.CodeFirst.InitTables(item);
            //    }
            //    catch (Exception ex)
            //    {
            //        log.Info(item);
            //        log.Error(ex);
            //    }
            //}

            MySqlControl.GetInstance().DB.CodeFirst.InitTables<MeasureImgResultModel>();
            _stopwatch.Stop();
            log.Info($"InitTables：{_stopwatch.Elapsed.TotalSeconds} 秒");
        }
        
    }
}
