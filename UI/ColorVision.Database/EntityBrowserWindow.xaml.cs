using ColorVision.Common.Utilities;
using ColorVision.Database.SqliteLog;
using ColorVision.Themes;
using log4net;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Database
{
    public partial class EntityBrowserWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(EntityBrowserWindow));

        private List<EntityTypeInfo> _allTypes = new();

        public EntityBrowserWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            LoadEntityTypes();
        }

        private void LoadEntityTypes()
        {
            try
            {
                var entityTypes = new List<EntityTypeInfo>();

                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray()!; }
                    catch { continue; }

                    foreach (var type in types)
                    {
                        if (type == null || type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
                            continue;
                        if (!typeof(IEntity).IsAssignableFrom(type))
                            continue;
                        if (type.GetConstructor(Type.EmptyTypes) == null)
                            continue;

                        var sugarTable = type.GetCustomAttribute<SugarTable>();
                        var dbType = DetectDbType(type);

                        entityTypes.Add(new EntityTypeInfo
                        {
                            Type = type,
                            DisplayName = type.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName
                                          ?? sugarTable?.TableDescription
                                          ?? type.Name,
                            TableName = sugarTable?.TableName ?? type.Name,
                            Namespace = type.Namespace ?? string.Empty,
                            DbType = dbType,
                            RecordCount = -1 // 延迟加载
                        });
                    }
                }

                _allTypes = entityTypes.OrderBy(t => t.DbType).ThenBy(t => t.TableName).ToList();
                EntityDataGrid.ItemsSource = _allTypes;

                // 异步加载记录数
                LoadRecordCountsAsync();

                log.InfoFormat("实体浏览器: 发现 {0} 个 IEntity 实现", _allTypes.Count);
            }
            catch (Exception ex)
            {
                log.Error("加载实体类型失败", ex);
                MessageBox.Show($"加载实体类型失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 根据 [DatabaseSource] 属性判断实体属于哪个数据库，默认 MySQL
        /// </summary>
        private static string DetectDbType(Type type)
        {
            var attr = type.GetCustomAttribute<DatabaseSourceAttribute>();
            if (attr != null)
                return attr.DatabaseType == DatabaseType.Sqlite ? "SQLite" : "MySQL";

            // 兜底：按命名空间推断
            var ns = type.Namespace ?? "";
            if (ns.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
                return "SQLite";
            return "MySQL";
        }

        /// <summary>
        /// 异步加载每个实体类型的记录数
        /// </summary>
        private async void LoadRecordCountsAsync()
        {
            await System.Threading.Tasks.Task.Run(() =>
            {
                foreach (var info in _allTypes)
                {
                    try
                    {
                        SqlSugarClient db = CreateDbForEntity(info);
                        var method = typeof(EntityBrowserWindow).GetMethod(nameof(GetCount), BindingFlags.NonPublic | BindingFlags.Static)!
                            .MakeGenericMethod(info.Type);
                        info.RecordCount = (int)method.Invoke(null, new object[] { db })!;
                        db.Dispose();
                    }
                    catch
                    {
                        info.RecordCount = -2; // 查询失败
                    }
                }
            });

            // 刷新 DataGrid 显示
            EntityDataGrid.Items.Refresh();
        }

        private static int GetCount<T>(SqlSugarClient db) where T : class, IEntity, new()
        {
            return db.Queryable<T>().Count();
        }

        private static SqlSugarClient CreateDbForEntity(EntityTypeInfo info)
        {
            if (info.DbType == "SQLite")
            {
                return SqliteLogManager.CreateDbClient();
            }
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = MySqlControl.GetConnectionString(),
                DbType = SqlSugar.DbType.MySql,
                IsAutoCloseConnection = true
            });
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var keyword = SearchBox.Text?.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                EntityDataGrid.ItemsSource = _allTypes;
                return;
            }

            EntityDataGrid.ItemsSource = _allTypes.Where(t =>
                t.TableName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                t.Type.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                t.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        private void EntityDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EntityDataGrid.SelectedItem is EntityTypeInfo info)
            {
                LoadCrudForType(info);
            }
        }

        private void LoadCrudForType(EntityTypeInfo info)
        {
            CrudContainer.Children.Clear();
            PlaceholderText.Visibility = Visibility.Collapsed;

            var db = CreateDbForEntity(info);
            var control = new EntityCrudControl(info.Type, db);
            CrudContainer.Children.Add(control);

            Title = $"实体浏览器 - {info.TableName} ({info.DbType})";
        }
    }

    public class EntityTypeInfo
    {
        public Type Type { get; set; }
        public string DisplayName { get; set; }
        public string TableName { get; set; }
        public string Namespace { get; set; }
        public string DbType { get; set; } = "MySQL";
        public int RecordCount { get; set; } = -1;

        public string RecordCountDisplay => RecordCount switch
        {
            -1 => "...",
            -2 => "错误",
            _ => RecordCount.ToString("N0")
        };

        public override string ToString() => $"{TableName} ({DbType})";
    }
}
