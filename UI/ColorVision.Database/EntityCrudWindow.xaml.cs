using ColorVision.Common.Utilities;
using ColorVision.Themes;
using log4net;
using SqlSugar;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ColorVision.Database
{
    public partial class EntityCrudWindow : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(EntityCrudWindow));

        // 数据访问对象和实体类型
        private readonly object _daoInstance;
        private readonly Type _entityType;
        private readonly string _tableName;

        // 分页配置
        private int _pageIndex = 1;
        private int _pageSize = 10;
        private int _totalCount;

        // 数据缓存
        private IList _allData;
        private IList _filteredData;

        // 搜索条件缓存
        private string _searchKeyword = "";

        /// <summary>
        /// 构造函数 - 使用泛型 DAO 实例
        /// </summary>
        /// <param name="daoInstance">BaseTableDao 实例，如 ConfigArchivedDao.Instance</param>
        /// <param name="entityType">实体类型，如 typeof(ConfigArchivedModel)</param>
        /// <param name="tableName">表名称（用于显示）</param>
        public EntityCrudWindow(object daoInstance, Type entityType, string tableName)
        {
            _daoInstance = daoInstance;
            _entityType = entityType;
            _tableName = tableName;
            _allData = new List<object>();
            _filteredData = new List<object>();

            InitializeComponent();
            this.Title = $"数据管理 - {tableName}";
            this.ApplyCaption();
        }

        /// <summary>
        /// 静态工厂方法 - 简化调用
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="daoInstance">DAO 单例实例</param>
        /// <param name="tableName">表名称</param>
        /// <returns></returns>
        public static EntityCrudWindow Create<T>(BaseTableDao<T> daoInstance, string tableName) where T : class, IEntity, new()
        {
            return new EntityCrudWindow(daoInstance, typeof(T), tableName);
        }

        public static EntityCrudWindow Create<T>(BaseTableDao<T> daoInstance) where T : class, IEntity, new()
        {
            var displayName = typeof(T).GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? typeof(T).Name;
            return new EntityCrudWindow(daoInstance, typeof(T), displayName);
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            SetupDataGrid();
            LoadData();
        }

        /// <summary>
        /// 配置 DataGrid 列
        /// </summary>
        private void SetupDataGrid()
        {
            EntityDataGrid.AutoGenerateColumns = false;
            EntityDataGrid.Columns.Clear();

            var properties = _entityType.GetProperties();
            foreach (var prop in properties)
            {
                // 检查是否应该显示
                var browsableAttr = prop.GetCustomAttribute<BrowsableAttribute>();
                if (browsableAttr != null && !browsableAttr.Browsable)
                    continue;

                var sugarColumn = prop.GetCustomAttribute<SugarColumn>();
                var displayNameAttr = prop.GetCustomAttribute<DisplayNameAttribute>();

                string header = displayNameAttr?.DisplayName ?? prop.Name;
                if (sugarColumn?.ColumnName != null)
                    header = displayNameAttr?.DisplayName ?? sugarColumn.ColumnName;

                DataGridColumn column;

                // 根据类型创建不同的列
                if (prop.PropertyType == typeof(bool) || prop.PropertyType == typeof(bool?))
                {
                    column = new DataGridCheckBoxColumn
                    {
                        Header = header,
                        Binding = new Binding(prop.Name),
                        IsReadOnly = true
                    };
                }
                else if (prop.PropertyType.IsEnum)
                {
                    column = new DataGridTextColumn
                    {
                        Header = header,
                        Binding = new Binding(prop.Name) { Converter = new EnumToDescriptionConverter() },
                        IsReadOnly = true
                    };
                }
                else if (prop.PropertyType == typeof(DateTime) || prop.PropertyType == typeof(DateTime?))
                {
                    column = new DataGridTextColumn
                    {
                        Header = header,
                        Binding = new Binding(prop.Name) { StringFormat = "yyyy-MM-dd HH:mm:ss" },
                        IsReadOnly = true
                    };
                }
                else
                {
                    column = new DataGridTextColumn
                    {
                        Header = header,
                        Binding = new Binding(prop.Name),
                        IsReadOnly = true
                    };
                }

                // 设置列宽
                if (prop.Name == "Id")
                {
                    column.Width = new DataGridLength(60);
                }
                else
                {
                    column.Width = DataGridLength.Auto;
                }

                EntityDataGrid.Columns.Add(column);
            }

            // 双击编辑
            EntityDataGrid.MouseDoubleClick += EntityDataGrid_MouseDoubleClick;
        }

        /// <summary>
        /// 加载数据
        /// </summary>
        private void LoadData()
        {
            try
            {
                StatusText.Text = "加载中...";

                // 通过反射调用 GetAll 方法
                var getAllMethod = _daoInstance.GetType().GetMethod("GetAll",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance,
                    null, new[] { typeof(int) }, null);

                if (getAllMethod != null)
                {
                    var result = getAllMethod.Invoke(_daoInstance, new object[] { -1 });
                    _allData = (IList)result ?? new List<object>();
                }
                else
                {
                    // 尝试调用扩展方法
                    var extensionMethod = typeof(BaseTableDaoExtensions).GetMethod("GetAll")
                        ?.MakeGenericMethod(_entityType);
                    if (extensionMethod != null)
                    {
                        var result = extensionMethod.Invoke(null, new[] { _daoInstance, -1 });
                        _allData = (IList)result ?? new List<object>();
                    }
                }

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

        /// <summary>
        /// 应用搜索过滤
        /// </summary>
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

                    var properties = item.GetType().GetProperties();
                    foreach (var prop in properties)
                    {
                        var value = prop.GetValue(item);
                        var text = value?.ToString();
                        if (!string.IsNullOrEmpty(text) && text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
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

        /// <summary>
        /// 刷新 DataGrid 显示（分页）
        /// </summary>
        private void RefreshDataGrid()
        {
            var pageData = new List<object>();
            int startIndex = (_pageIndex - 1) * _pageSize;
            int endIndex = Math.Min(startIndex + _pageSize, _filteredData.Count);

            for (int i = startIndex; i < endIndex; i++)
            {
                var item = _filteredData[i];
                if (item != null)
                {
                    pageData.Add(item);
                }
            }

            EntityDataGrid.ItemsSource = pageData;
        }

        /// <summary>
        /// 更新分页控件
        /// </summary>
        private void UpdatePagination()
        {
            int totalPages = (_totalCount + _pageSize - 1) / _pageSize;
            if (totalPages < 1) totalPages = 1;

            if (_pageIndex > totalPages) _pageIndex = totalPages;
            if (_pageIndex < 1) _pageIndex = 1;

            PageNumberTextBox.Text = _pageIndex.ToString();
            TotalPagesTextBlock.Text = totalPages.ToString();
        }

        /// <summary>
        /// 更新状态显示
        /// </summary>
        private void UpdateStatus()
        {
            RecordCountText.Text = $"共 {_totalCount} 条记录";
            StatusText.Text = "就绪";
        }

        #region 事件处理

        /// <summary>
        /// 双击编辑
        /// </summary>
        private void EntityDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (EntityDataGrid.SelectedItem != null)
            {
                EditSelectedItem();
            }
        }

        /// <summary>
        /// 新增按钮
        /// </summary>
        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var newEntity = Activator.CreateInstance(_entityType);
            if (newEntity == null)
            {
                StatusText.Text = "无法创建实体实例";
                return;
            }

            var editWindow = new EntityEditWindow(newEntity, $"新增 {_tableName}");
            editWindow.Owner = this;
            editWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            if (editWindow.ShowDialog() == true)
            {
                SaveEntity(newEntity);
            }
        }

        /// <summary>
        /// 编辑按钮
        /// </summary>
        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            EditSelectedItem();
        }

        /// <summary>
        /// 编辑选中项
        /// </summary>
        private void EditSelectedItem()
        {
            if (EntityDataGrid.SelectedItem == null)
            {
                StatusText.Text = "请先选择一条记录";
                return;
            }

            // 创建副本进行编辑
            var original = EntityDataGrid.SelectedItem;
            var clone = Activator.CreateInstance(_entityType);
            if (clone == null)
            {
                StatusText.Text = "无法创建实体实例";
                return;
            }

            CopyProperties(original, clone);

            var editWindow = new EntityEditWindow(clone, $"编辑 {_tableName}");
            editWindow.Owner = this;
            editWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            if (editWindow.ShowDialog() == true)
            {
                SaveEntity(clone);
            }
        }

        /// <summary>
        /// 删除按钮
        /// </summary>
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (EntityDataGrid.SelectedItem == null)
            {
                StatusText.Text = "请先选择一条记录";
                return;
            }

            var entity = EntityDataGrid.SelectedItem as IEntity;
            if (entity == null)
            {
                StatusText.Text = "无法获取记录ID";
                return;
            }

            var result = MessageBox.Show($"确定要删除 ID={entity.Id} 的记录吗？", "确认删除",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // 反射调用 DeleteById
                    var deleteMethod = typeof(BaseTableDaoExtensions).GetMethod("DeleteById")
                        ?.MakeGenericMethod(_entityType);
                    if (deleteMethod != null)
                    {
                        deleteMethod.Invoke(null, new[] { _daoInstance, entity.Id });
                        StatusText.Text = $"删除成功 ID={entity.Id}";
                        log.InfoFormat("删除表 {0} 记录 ID={1}", _tableName, entity.Id);
                        LoadData();
                    }
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"删除失败: {ex.Message}";
                    log.ErrorFormat("删除表 {0} 记录失败: {1}", _tableName, ex.Message);
                }
            }
        }

        /// <summary>
        /// 刷新按钮
        /// </summary>
        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        /// <summary>
        /// 搜索按钮
        /// </summary>
        private void Search_Click(object sender, RoutedEventArgs e)
        {
            _searchKeyword = SearchTextBox.Text;
            _pageIndex = 1;
            ApplyFilter();
            UpdateStatus();
        }

        /// <summary>
        /// 清空搜索
        /// </summary>
        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = "";
            _searchKeyword = "";
            _pageIndex = 1;
            ApplyFilter();
            UpdateStatus();
        }

        /// <summary>
        /// 保存实体
        /// </summary>
        private void SaveEntity(object entity)
        {
            try
            {
                // 反射调用 Save
                var saveMethod = typeof(BaseTableDaoExtensions).GetMethod("Save")
                    ?.MakeGenericMethod(_entityType);
                if (saveMethod != null)
                {
                    var result = saveMethod.Invoke(null, new[] { _daoInstance, entity });
                    int affectedRows = Convert.ToInt32(result);

                    if (affectedRows > 0)
                    {
                        var idProperty = entity.GetType().GetProperty("Id");
                        var id = idProperty?.GetValue(entity);
                        StatusText.Text = $"保存成功 ID={id}";
                        log.InfoFormat("保存表 {0} 记录成功 ID={1}", _tableName, id);
                        LoadData();
                    }
                    else
                    {
                        StatusText.Text = "保存失败";
                    }
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"保存失败: {ex.Message}";
                log.ErrorFormat("保存表 {0} 记录失败: {1}", _tableName, ex.Message);
            }
        }

        /// <summary>
        /// 复制属性
        /// </summary>
        private void CopyProperties(object source, object target)
        {
            var properties = _entityType.GetProperties();
            foreach (var prop in properties)
            {
                if (prop.CanRead && prop.CanWrite)
                {
                    var value = prop.GetValue(source);
                    prop.SetValue(target, value);
                }
            }
        }

        #endregion

        #region 分页事件

        private void FirstPage_Click(object sender, RoutedEventArgs e)
        {
            _pageIndex = 1;
            UpdatePagination();
            RefreshDataGrid();
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_pageIndex > 1)
            {
                _pageIndex--;
                UpdatePagination();
                RefreshDataGrid();
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            int totalPages = int.Parse(TotalPagesTextBlock.Text);
            if (_pageIndex < totalPages)
            {
                _pageIndex++;
                UpdatePagination();
                RefreshDataGrid();
            }
        }

        private void LastPage_Click(object sender, RoutedEventArgs e)
        {
            int totalPages = int.Parse(TotalPagesTextBlock.Text);
            _pageIndex = totalPages;
            UpdatePagination();
            RefreshDataGrid();
        }

        private void PageSize_Changed(object sender, SelectionChangedEventArgs e)
        {
            switch (PageSizeComboBox.SelectedIndex)
            {
                case 0: _pageSize = 10; break;
                case 1: _pageSize = 20; break;
                case 2: _pageSize = 50; break;
                case 3: _pageSize = 100; break;
            }
            _pageIndex = 1;
            UpdatePagination();
            RefreshDataGrid();
        }

        #endregion
    }

    /// <summary>
    /// 枚举转描述转换器
    /// </summary>
    public class EnumToDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null) return string.Empty;

            var type = value.GetType();
            if (!type.IsEnum) return value.ToString() ?? string.Empty;

            var enumName = value.ToString();
            if (string.IsNullOrWhiteSpace(enumName)) return string.Empty;

            var field = type.GetField(enumName);
            var attr = field?.GetCustomAttribute<DescriptionAttribute>();
            return attr?.Description ?? enumName;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
