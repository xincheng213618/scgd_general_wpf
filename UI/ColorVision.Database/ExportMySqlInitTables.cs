#pragma warning disable CA1822
using ColorVision.UI;
using ColorVision.Database.Properties;
using ColorVision.UI.Menus;
using log4net;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Database
{
    public interface IInitTables
    {

    }

    public static class MySqlTableInitializer
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MySqlTableInitializer));

        public static async Task InitializeWithNotificationAsync(Window? owner = null)
        {
            try
            {
                await InitializeAsync();
                log.Info("Mysql表初始化完成");
                ShowMessage(owner, Resources.DB_InitSuccess, Resources.DB_Success, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                log.Error("初始化Mysql表失败", ex);
                ShowMessage(owner, Resources.DB_InitFailed, Resources.DB_Error, MessageBoxImage.Error);
            }
        }

        public static Task InitializeAsync() => Task.Run(Initialize);

        private static void Initialize()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            IEnumerable<Type> tableTypes = AssemblyHandler.GetInstance().GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => typeof(IInitTables).IsAssignableFrom(type) && !type.IsAbstract);
            InitTableTypes(tableTypes, type =>
            {
                using var DB = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                DB.CodeFirst.InitTables(type);
            });

            stopwatch.Stop();
            log.Info($"InitTables：{stopwatch.Elapsed.TotalSeconds} 秒");
        }

        internal static void InitTableTypes(IEnumerable<Type> tableTypes, Action<Type> initializer)
        {
            ArgumentNullException.ThrowIfNull(tableTypes);
            ArgumentNullException.ThrowIfNull(initializer);

            List<Exception> failures = new();
            foreach (Type type in tableTypes)
            {
                try
                {
                    log.Info($"正在初始化表：{type.Name}");
                    initializer(type);
                }
                catch (Exception ex)
                {
                    log.Error($"初始化表 {type.FullName} 失败", ex);
                    failures.Add(new InvalidOperationException($"初始化表 {type.FullName} 失败。", ex));
                }
            }

            if (failures.Count > 0)
            {
                throw new AggregateException("一个或多个 MySQL 表初始化失败。", failures);
            }
        }

        private static void ShowMessage(Window? owner, string message, string caption, MessageBoxImage image)
        {
            if (owner == null)
                MessageBox.Show(message, caption, MessageBoxButton.OK, image);
            else
                MessageBox.Show(owner, message, caption, MessageBoxButton.OK, image);
        }
    }

    [Obsolete("MySQL table initialization is available from the MySQL tool window.")]
    public class ExportMySqlInitTables : MenuItemBase
    {
        public override string OwnerGuid => "ExportMySqlMenuItem";
        public override string GuidId => nameof(ExportMySqlInitTables);
        public override string Header => Resources.MenuMySqlInitTables;
        public override int Order => 2;

        public override async void Execute()
        {
            await MySqlTableInitializer.InitializeWithNotificationAsync(WindowHelpers.GetActiveWindow());
        }
    }
}
