using log4net;
using SqlSugar;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ColorVision.Database
{
    /// <summary>
    /// 通用实体 CRUD 控件 - 类似 Navicat 的增删改查界面
    /// 支持 MySQL 和 SQLite，只需传入 Type + SqlSugarClient
    /// </summary>
    public partial class EntityCrudControl : UserControl
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(EntityCrudControl));

        private readonly Type _entityType;
        private readonly SqlSugarClient _db;
        private readonly string _tableName;

        // 分页
        private int _pageIndex = 1;
        private int _pageSize = 20;
        private int _totalCount;

        // 数据
        private IList _allData;
        private IList _filteredData;
        private string _searchKeyword = "";

        /// <param name="entityType">实体类型</param>
        /// <param name="db">数据库连接（MySQL 或 SQLite）</param>
        public EntityCrudControl(Type entityType, SqlSugarClient db)
        {
            _entityType = entityType;
            _db = db;
            _tableName = entityType.GetCustomAttribute<SugarTable>()?.TableName ?? entityType.Name;
            _allData = new List<object>();
            _filteredData = new List<object>();

            InitializeComponent();
            SetupDataGrid();
            Loaded += (_, _) => LoadData();
        }

        /// <summary>
        /// 兼容旧调用 - 默认使用 MySQL
        /// </summary>
        public EntityCrudControl(Type entityType)
            : this(entityType, CreateMySqlDb()) { }

        #region DataGrid 配置

        private void SetupDataGrid()
        {
            EntityDataGrid.AutoGenerateColumns = false;
            EntityDataGrid.Columns.Clear();

            var properties = _entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                var browsable = prop.GetCustomAttribute<BrowsableAttribute>();
                if (browsable != null && !browsable.Browsable) continue;

                var sugarColumn = prop.GetCustomAttribute<SugarColumn>();
                if (sugarColumn?.IsIgnore == true) continue;

                var displayNameAttr = prop.GetCustomAttribute<DisplayNameAttribute>();
                string header = displayNameAttr?.DisplayName ?? sugarColumn?.ColumnName ?? prop.Name;

                // Id 列和主键/自增列只读
                bool isReadOnly = prop.Name == "Id" || sugarColumn?.IsPrimaryKey == true || sugarColumn?.IsIdentity == true;

                DataGridColumn column;
                if (prop.PropertyType == typeof(bool) || prop.PropertyType == typeof(bool?))
                    column = new DataGridCheckBoxColumn { Header = header, Binding = new Binding(prop.Name), IsReadOnly = isReadOnly };
                else if (prop.PropertyType.IsEnum)
                    column = new DataGridTextColumn { Header = header, Binding = new Binding(prop.Name) { Converter = new EnumToDescriptionConverter() }, IsReadOnly = isReadOnly };
                else if (prop.PropertyType == typeof(DateTime) || prop.PropertyType == typeof(DateTime?))
                    column = new DataGridTextColumn { Header = header, Binding = new Binding(prop.Name) { StringFormat = "yyyy-MM-dd HH:mm:ss" }, IsReadOnly = isReadOnly };
                else
                    column = new DataGridTextColumn { Header = header, Binding = new Binding(prop.Name) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus }, IsReadOnly = isReadOnly };

                column.Width = prop.Name == "Id" ? new DataGridLength(60) : DataGridLength.Auto;
                EntityDataGrid.Columns.Add(column);
            }

            EntityDataGrid.MouseDoubleClick += (_, e) => { if (EntityDataGrid.SelectedItem != null) EditSelectedItem(); };
        }

        /// <summary>
        /// 单元格编辑完成 → 自动保存
        /// </summary>
        private void EntityDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;

            var entity = e.Row.Item;
            if (entity == null) return;

            // 延迟保存：等 Binding 更新源
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var result = SaveEntity(entity);
                if (result > 0)
                {
                    StatusText.Text = "自动保存成功";
                    // 刷新分页数据（不重新加载全部数据，避免闪烁）
                    RefreshDataGrid();
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        #endregion

        #region 数据加载

        private void LoadData()
        {
            try
            {
                StatusText.Text = "加载中...";
                var method = typeof(EntityCrudControl).GetMethod(nameof(QueryAll), BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(_entityType);
                _allData = (IList)method.Invoke(this, null)!;

                ApplyFilter();
                UpdateStatus();
                log.InfoFormat("加载表 {0} 数据成功，共 {1} 条", _tableName, _allData.Count);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"加载失败: {ex.Message}";
                log.ErrorFormat("加载表 {0} 数据失败: {1}", _tableName, ex.Message);
            }
        }

        private List<T> QueryAll<T>() where T : class, IEntity, new()
        {
            return _db.Queryable<T>().OrderBy(x => x.Id, OrderByType.Desc).ToList();
        }

        private void ApplyFilter()
        {
            if (string.IsNullOrWhiteSpace(_searchKeyword))
            {
                _filteredData = _allData;
            }
            else
            {
                var keyword = _searchKeyword.Trim();
                var filtered = new List<object>();
                foreach (var item in _allData)
                {
                    if (item == null) continue;
                    foreach (var prop in item.GetType().GetProperties())
                    {
                        var value = prop.GetValue(item);
                        if (value?.ToString()?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            filtered.Add(item);
                            break;
                        }
                    }
                }
                _filteredData = filtered;
            }

            _totalCount = _filteredData.Count;
            UpdatePagination();
            RefreshDataGrid();
        }

        private void RefreshDataGrid()
        {
            var pageData = new List<object>();
            int start = (_pageIndex - 1) * _pageSize;
            int end = Math.Min(start + _pageSize, _filteredData.Count);
            for (int i = start; i < end; i++)
                if (_filteredData[i] != null) pageData.Add(_filteredData[i]);

            EntityDataGrid.ItemsSource = pageData;
        }

        private void UpdatePagination()
        {
            int totalPages = Math.Max(1, (_totalCount + _pageSize - 1) / _pageSize);
            if (_pageIndex > totalPages) _pageIndex = totalPages;
            if (_pageIndex < 1) _pageIndex = 1;
            PageNumberTextBox.Text = _pageIndex.ToString();
            TotalPagesTextBlock.Text = totalPages.ToString();
        }

        private void UpdateStatus()
        {
            RecordCountText.Text = $"共 {_totalCount} 条记录";
            StatusText.Text = "就绪";
        }

        #endregion

        #region CRUD 操作

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var entity = Activator.CreateInstance(_entityType);
            if (entity == null) return;

            var win = new EntityEditWindow(entity, $"新增 {_tableName}") { Owner = Window.GetWindow(this) };
            win.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            if (win.ShowDialog() == true)
            {
                if (SaveEntity(entity) > 0) LoadData();
            }
        }

        private void Edit_Click(object sender, RoutedEventArgs e) => EditSelectedItem();

        private void EditSelectedItem()
        {
            var selected = EntityDataGrid.SelectedItem;
            if (selected == null) { StatusText.Text = "请先选择一条记录"; return; }

            var clone = Activator.CreateInstance(_entityType);
            if (clone == null) return;
            CopyProperties(selected, clone);

            var win = new EntityEditWindow(clone, $"编辑 {_tableName}") { Owner = Window.GetWindow(this) };
            win.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            if (win.ShowDialog() == true)
            {
                if (SaveEntity(clone) > 0) LoadData();
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (EntityDataGrid.SelectedItem is not IEntity entity)
            {
                StatusText.Text = "请先选择一条记录";
                return;
            }

            if (MessageBox.Show($"确定要删除 ID={entity.Id} 的记录吗？", "确认删除",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                var method = typeof(EntityCrudControl).GetMethod(nameof(DeleteById), BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(_entityType);
                var result = (int)method.Invoke(this, new object[] { entity.Id })!;
                if (result > 0)
                {
                    StatusText.Text = $"删除成功 ID={entity.Id}";
                    LoadData();
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"删除失败: {ex.Message}";
                log.ErrorFormat("删除表 {0} 记录失败: {1}", _tableName, ex.Message);
            }
        }

        private int DeleteById<T>(int id) where T : class, IEntity, new()
        {
            return _db.Deleteable<T>().Where(x => x.Id == id).ExecuteCommand();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => LoadData();

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            _searchKeyword = SearchTextBox.Text;
            _pageIndex = 1;
            ApplyFilter();
            UpdateStatus();
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = "";
            _searchKeyword = "";
            _pageIndex = 1;
            ApplyFilter();
            UpdateStatus();
        }

        private int SaveEntity(object entity)
        {
            try
            {
                var method = typeof(EntityCrudControl).GetMethod(nameof(SaveItem), BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(_entityType);
                var result = (int)method.Invoke(this, new[] { entity })!;
                if (result > 0)
                {
                    var id = _entityType.GetProperty("Id")?.GetValue(entity);
                    StatusText.Text = $"保存成功 ID={id}";
                }
                else
                {
                    StatusText.Text = "保存失败";
                }
                return result;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"保存失败: {ex.Message}";
                log.ErrorFormat("保存表 {0} 记录失败: {1}", _tableName, ex.Message);
                return -1;
            }
        }

        private int SaveItem<T>(T entity) where T : class, IEntity, new()
        {
            if (entity.Id <= 0)
            {
                var newId = _db.Insertable(entity).ExecuteReturnIdentity();
                entity.Id = newId;
                return 1;
            }
            else
            {
                return _db.Updateable(entity).Where(x => x.Id == entity.Id).ExecuteCommand();
            }
        }

        #endregion

        #region 分页事件

        private void FirstPage_Click(object sender, RoutedEventArgs e) { _pageIndex = 1; UpdatePagination(); RefreshDataGrid(); }
        private void PrevPage_Click(object sender, RoutedEventArgs e) { if (_pageIndex > 1) { _pageIndex--; UpdatePagination(); RefreshDataGrid(); } }
        private void NextPage_Click(object sender, RoutedEventArgs e) { int total = int.Parse(TotalPagesTextBlock.Text); if (_pageIndex < total) { _pageIndex++; UpdatePagination(); RefreshDataGrid(); } }
        private void LastPage_Click(object sender, RoutedEventArgs e) { _pageIndex = int.Parse(TotalPagesTextBlock.Text); UpdatePagination(); RefreshDataGrid(); }

        private void PageSize_Changed(object sender, SelectionChangedEventArgs e)
        {
            _pageSize = PageSizeComboBox.SelectedIndex switch { 0 => 10, 1 => 20, 2 => 50, 3 => 100, _ => 20 };
            _pageIndex = 1;
            UpdatePagination();
            RefreshDataGrid();
        }

        #endregion

        #region 辅助方法

        private static SqlSugarClient CreateMySqlDb()
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = MySqlControl.GetConnectionString(),
                DbType = SqlSugar.DbType.MySql,
                IsAutoCloseConnection = true
            });
        }

        private void CopyProperties(object source, object target)
        {
            foreach (var prop in _entityType.GetProperties())
                if (prop.CanRead && prop.CanWrite)
                    prop.SetValue(target, prop.GetValue(source));
        }

        #endregion
    }

    /// <summary>
    /// 枚举转描述转换器
    /// </summary>
    public class EnumToDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;
            var type = value.GetType();
            if (!type.IsEnum) return value.ToString() ?? string.Empty;
            var enumName = value.ToString();
            if (string.IsNullOrWhiteSpace(enumName)) return string.Empty;
            var field = type.GetField(enumName);
            return field?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? enumName;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
