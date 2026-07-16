#pragma warning disable CA1863
using ColorVision.Themes;
using ColorVision.UI;
using log4net;
using ColorVision.Database.Properties;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ColorVision.Database
{
    public partial class DatabaseBrowserWindow : Window, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DatabaseBrowserWindow));

        private readonly ObservableCollection<DatabaseTreeNode> _nodes = new();
        private CancellationTokenSource? _loadCancellation;
        private IDatabaseBrowserProvider? _currentProvider;
        private DatabaseTableInfo? _currentTable;
        private IReadOnlyList<DatabaseColumnInfo> _currentColumns = Array.Empty<DatabaseColumnInfo>();
        private DataTable? _currentDataTable;
        private string _searchKeyword = string.Empty;
        private string? _sortColumn;
        private ListSortDirection _sortDirection = ListSortDirection.Descending;
        private int _pageIndex = 1;
        private int _pageSize = 50;
        private int _totalCount;
        private bool _hasLoadedPage;
        private bool _isDisposed;
        private CopilotDynamicContextSession? _copilotContextSession;

        public DatabaseBrowserWindow()
        {
            InitializeComponent();
            DataContext = _nodes;
        }

        private int TotalPages => Math.Max(1, (_totalCount + _pageSize - 1) / _pageSize);
        private bool HasPrimaryKey => _currentColumns.Any(column => column.IsPrimaryKey);
        private bool CanWriteCurrentTable => _currentProvider?.CanWrite == true && _currentTable?.CanWrite == true;

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.ApplyCaption();
            try
            {
                _copilotContextSession = CopilotDatabaseContextHub.Shared.Register(
                    CaptureCopilotDatabaseSnapshotAsync,
                    typeof(DatabaseBrowserWindow).Assembly.GetName().Version?.ToString());
            }
            catch (Exception ex)
            {
                log.Warn("注册数据库浏览器 Copilot 上下文失败，数据库浏览器将继续运行", ex);
            }
            LoadProviders();
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            _copilotContextSession?.Activate();
            PublishCopilotContext();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            Dispose();
        }

        private void LoadProviders()
        {
            _nodes.Clear();
            ClearTableView(Properties.Resources.DB_SelectTable, Properties.Resources.DB_ExpandHint, Properties.Resources.DB_Ready);

            try
            {
                var providers = DatabaseBrowserProviderRegistry.GetProviders();
                foreach (var provider in providers)
                {
                    _nodes.Add(DatabaseTreeNode.CreateProvider(provider));
                }

                ProviderSummaryText.Text = $"共 {providers.Count} 个数据源";
                StatusText.Text = providers.Count == 0 ? Properties.Resources.DB_NoDataSource : Properties.Resources.DB_Ready;
            }
            catch (Exception ex)
            {
                ProviderSummaryText.Text = Properties.Resources.DB_LoadFailed;
                StatusText.Text = GetBaseMessage(ex);
                log.Error("加载数据库浏览器数据源失败", ex);
            }
        }

        private async void BrowserTreeItem_Expanded(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not TreeViewItem item || item.DataContext is not DatabaseTreeNode node)
                return;

            if (node.IsLoaded || node.NodeType == DatabaseTreeNodeType.Loading || node.NodeType == DatabaseTreeNodeType.Error)
                return;

            e.Handled = true;
            await LoadNodeChildrenAsync(node);
        }

        private async Task LoadNodeChildrenAsync(DatabaseTreeNode node)
        {
            node.Children.Clear();
            node.Children.Add(DatabaseTreeNode.CreateLoading());

            try
            {
                if (node.NodeType == DatabaseTreeNodeType.Provider && node.Provider != null)
                {
                    var provider = node.Provider;
                    var databases = await Task.Run(provider.GetDatabases);
                    node.Children.Clear();

                    foreach (var database in databases)
                        node.Children.Add(DatabaseTreeNode.CreateCatalog(provider, database));

                    if (databases.Count == 0)
                        node.Children.Add(DatabaseTreeNode.CreateError(Properties.Resources.DB_NoDatabase));
                }
                else if (node.NodeType == DatabaseTreeNodeType.Catalog && node.Provider != null && node.Catalog != null)
                {
                    var provider = node.Provider;
                    var catalog = node.Catalog;
                    var tables = await Task.Run(() => provider.GetTables(catalog.Name));
                    node.Children.Clear();

                    foreach (var table in tables)
                        node.Children.Add(DatabaseTreeNode.CreateTable(provider, catalog, table));

                    if (tables.Count == 0)
                        node.Children.Add(DatabaseTreeNode.CreateError(Properties.Resources.DB_NoTable));
                }

                node.IsLoaded = true;
            }
            catch (Exception ex)
            {
                node.Children.Clear();
                node.Children.Add(DatabaseTreeNode.CreateError(GetBaseMessage(ex)));
                StatusText.Text = GetBaseMessage(ex);
                log.Warn($"加载节点 {node.Header} 失败", ex);
            }
        }

        private async void BrowserTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is not DatabaseTreeNode node)
                return;

            if (node.NodeType != DatabaseTreeNodeType.Table || node.Provider == null || node.Table == null)
            {
                ClearTableView(Properties.Resources.DB_SelectTable, Properties.Resources.DB_ExpandHint, Properties.Resources.DB_Ready);
                return;
            }

            _currentProvider = node.Provider;
            _currentTable = node.Table;
            _currentColumns = Array.Empty<DatabaseColumnInfo>();
            _currentDataTable = null;
            _totalCount = 0;
            _hasLoadedPage = false;
            RowsDataGrid.ItemsSource = null;
            PlaceholderText.Visibility = Visibility.Visible;
            _pageIndex = 1;
            _searchKeyword = SearchBox.Text?.Trim() ?? string.Empty;
            _sortColumn = null;
            _sortDirection = ListSortDirection.Descending;
            PublishCopilotContext();
            await LoadCurrentTableAsync();
        }

        private async Task LoadCurrentTableAsync()
        {
            if (_currentProvider == null || _currentTable == null)
                return;

            CancelCurrentLoad();
            _loadCancellation = new CancellationTokenSource();
            var token = _loadCancellation.Token;
            var provider = _currentProvider;
            var table = _currentTable;

            try
            {
                SetBusy(true, $"{Properties.Resources.DB_Loading} {table.TableName}...");
                TableTitleText.Text = table.TableName;
                TableSourceText.Text = $"{table.ProviderName} / {table.DatabaseName}";
                TableMetaText.Text = string.Empty;

                var columns = await Task.Run(() => provider.GetColumns(table), token);
                token.ThrowIfCancellationRequested();
                _currentColumns = columns;
                var defaultSortColumn = columns.FirstOrDefault(column => column.IsPrimaryKey);
                if (defaultSortColumn == null && columns.Count > 0)
                    defaultSortColumn = columns[0];
                _sortColumn ??= defaultSortColumn?.ColumnName;

                UpdateWriteState();
                await LoadPageAsync(token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                ClearTableView(table.TableName, $"{table.ProviderName} / {table.DatabaseName}", $"{Properties.Resources.DB_LoadFailed}: {GetBaseMessage(ex)}");
                log.Error($"加载表 {table.TableName} 失败", ex);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task LoadPageAsync(CancellationToken token)
        {
            if (_currentProvider == null || _currentTable == null)
                return;

            var provider = _currentProvider;
            var table = _currentTable;
            var pageIndex = _pageIndex;
            var pageSize = _pageSize;
            var keyword = _searchKeyword;
            var sortColumn = _sortColumn;
            var sortDirection = _sortDirection;

            _hasLoadedPage = false;
            PublishCopilotContext();
            SetBusy(true, Properties.Resources.DB_Querying);
            var page = await Task.Run(() => provider.QueryPage(table, pageIndex, pageSize, keyword, sortColumn, sortDirection), token);
            token.ThrowIfCancellationRequested();

            _currentDataTable = page.Rows;
            _currentDataTable.AcceptChanges();
            _totalCount = page.TotalCount;
            _hasLoadedPage = true;
            RowsDataGrid.ItemsSource = _currentDataTable.DefaultView;
            PlaceholderText.Visibility = _currentDataTable.Rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            UpdatePagination();
            UpdateWriteState();
            TableMetaText.Text = $"{_currentColumns.Count} 列" + (HasPrimaryKey ? "" : " / 无主键");
            RecordCountText.Text = string.Format(Properties.Resources.DB_RecordCount, _totalCount.ToString("N0"));
            StatusText.Text = Properties.Resources.DB_Ready;
            PublishCopilotContext();
        }

        private void RowsDataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            var columnInfo = _currentColumns.FirstOrDefault(column => string.Equals(column.ColumnName, e.PropertyName, StringComparison.OrdinalIgnoreCase));
            if (columnInfo == null)
                return;

            e.Column.Header = string.IsNullOrWhiteSpace(columnInfo.DisplayName) ? columnInfo.ColumnName : columnInfo.DisplayName;
            e.Column.SortMemberPath = columnInfo.ColumnName;
            e.Column.IsReadOnly = !CanWriteCurrentTable || !HasPrimaryKey || columnInfo.IsPrimaryKey || columnInfo.IsIdentity || columnInfo.IsReadOnly;
            e.Column.HeaderStyle = CreateHeaderStyle(columnInfo);
        }

        private void RowsDataGrid_AutoGeneratedColumns(object sender, EventArgs e)
        {
            foreach (var column in RowsDataGrid.Columns)
                column.SortDirection = null;

            var sortedColumn = RowsDataGrid.Columns.FirstOrDefault(column => string.Equals(column.SortMemberPath, _sortColumn, StringComparison.OrdinalIgnoreCase));
            if (sortedColumn != null)
                sortedColumn.SortDirection = _sortDirection;
        }

        private static Style CreateHeaderStyle(DatabaseColumnInfo columnInfo)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(columnInfo.StoreType)) parts.Add(columnInfo.StoreType);
            if (columnInfo.IsPrimaryKey) parts.Add("Primary Key");
            if (columnInfo.IsIdentity) parts.Add("Identity");
            if (!columnInfo.IsNullable) parts.Add("Not Null");
            if (!string.IsNullOrWhiteSpace(columnInfo.Comment)) parts.Add(columnInfo.Comment);

            var style = new Style(typeof(DataGridColumnHeader));
            if (parts.Count > 0)
                style.Setters.Add(new Setter(ToolTipProperty, string.Join(Environment.NewLine, parts)));
            return style;
        }

        private async void RowsDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            if (_currentProvider == null || _currentTable == null)
                return;

            var sortColumn = e.Column.SortMemberPath;
            if (string.IsNullOrWhiteSpace(sortColumn))
                return;

            if (string.Equals(_sortColumn, sortColumn, StringComparison.OrdinalIgnoreCase))
            {
                _sortDirection = _sortDirection == ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;
            }
            else
            {
                _sortColumn = sortColumn;
                _sortDirection = ListSortDirection.Ascending;
            }

            _pageIndex = 1;
            await ReloadPageAsync();
        }

        private async void AddRow_Click(object sender, RoutedEventArgs e)
        {
            if (_currentProvider == null || _currentTable == null || !CanWriteCurrentTable)
                return;

            var window = new DatabaseRowEditWindow(_currentColumns, $"新增 {_currentTable.TableName}")
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (window.ShowDialog() != true)
                return;

            try
            {
                SetBusy(true, Properties.Resources.DB_Inserting);
                var provider = _currentProvider;
                var table = _currentTable;
                var values = window.Values;
                var affected = await Task.Run(() => provider.InsertRow(table, values));
                StatusText.Text = affected > 0 ? Properties.Resources.DB_InsertSuccess : "新增未影响任何记录";
                _pageIndex = 1;
                await ReloadPageAsync();
            }
            catch (Exception ex)
            {
                StatusText.Text = string.Format(Properties.Resources.DB_InsertFailed, GetBaseMessage(ex));
                log.Error("新增数据库行失败", ex);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void SaveChanges_Click(object sender, RoutedEventArgs e)
        {
            await SaveChangesAsync();
        }

        private async Task SaveChangesAsync()
        {
            if (_currentProvider == null || _currentTable == null || _currentDataTable == null || !CanWriteCurrentTable)
                return;

            RowsDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            RowsDataGrid.CommitEdit(DataGridEditingUnit.Row, true);

            var changedRows = _currentDataTable.Rows
                .Cast<DataRow>()
                .Where(row => row.RowState is DataRowState.Added or DataRowState.Modified)
                .ToList();

            if (changedRows.Count == 0)
            {
                StatusText.Text = "没有需要保存的更改";
                return;
            }

            if (changedRows.Any(row => row.RowState == DataRowState.Modified) && !HasPrimaryKey)
            {
                StatusText.Text = "当前表没有主键，不能保存修改。";
                return;
            }

            try
            {
                SetBusy(true, Properties.Resources.DB_Saving);
                var provider = _currentProvider;
                var table = _currentTable;
                var columns = _currentColumns;
                var affected = await Task.Run(() =>
                {
                    var total = 0;
                    foreach (var row in changedRows)
                    {
                        if (row.RowState == DataRowState.Added)
                        {
                            total += provider.InsertRow(table, BuildValues(row, columns));
                        }
                        else if (row.RowState == DataRowState.Modified)
                        {
                            total += provider.UpdateRow(table, BuildKeys(row, columns, DataRowVersion.Original), BuildValues(row, columns));
                        }
                    }

                    return total;
                });

                StatusText.Text = string.Format(Properties.Resources.DB_SaveComplete, affected.ToString("N0"));
                await ReloadPageAsync();
            }
            catch (Exception ex)
            {
                StatusText.Text = string.Format(Properties.Resources.DB_SaveFailed, GetBaseMessage(ex));
                log.Error("保存数据库行失败", ex);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void RevertChanges_Click(object sender, RoutedEventArgs e)
        {
            _currentDataTable?.RejectChanges();
            StatusText.Text = Properties.Resources.DB_Undone;
            PublishCopilotContext();
        }

        private async void DeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if (_currentProvider == null || _currentTable == null || !CanWriteCurrentTable || !HasPrimaryKey)
                return;

            RowsDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            RowsDataGrid.CommitEdit(DataGridEditingUnit.Row, true);

            if (RowsDataGrid.SelectedItem is not DataRowView rowView)
            {
                StatusText.Text = Properties.Resources.DB_SelectRowFirst;
                return;
            }

            if (MessageBox.Show(this, string.Format(Properties.Resources.DB_ConfirmDeleteRows, _currentTable.TableName), Properties.Resources.DB_ConfirmDelete, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                SetBusy(true, Properties.Resources.DB_Deleting);
                var provider = _currentProvider;
                var table = _currentTable;
                var keys = BuildKeys(rowView.Row, _currentColumns, rowView.Row.RowState == DataRowState.Modified ? DataRowVersion.Original : DataRowVersion.Current);
                var affected = await Task.Run(() => provider.DeleteRow(table, keys));
                StatusText.Text = affected > 0 ? Properties.Resources.DB_DeleteSuccess : "未删除任何记录";
                await ReloadPageAsync();
            }
            catch (Exception ex)
            {
                StatusText.Text = string.Format(Properties.Resources.DB_DeleteFailed, GetBaseMessage(ex));
                log.Error("删除数据库行失败", ex);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void RefreshTable_Click(object sender, RoutedEventArgs e)
        {
            await ReloadPageAsync();
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            _searchKeyword = SearchBox.Text?.Trim() ?? string.Empty;
            _pageIndex = 1;
            await ReloadPageAsync();
        }

        private async void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = string.Empty;
            _searchKeyword = string.Empty;
            _pageIndex = 1;
            await ReloadPageAsync();
        }

        private async void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            _searchKeyword = SearchBox.Text?.Trim() ?? string.Empty;
            _pageIndex = 1;
            await ReloadPageAsync();
        }

        private void RefreshProviders_Click(object sender, RoutedEventArgs e)
        {
            LoadProviders();
        }

        private async void FirstPage_Click(object sender, RoutedEventArgs e)
        {
            _pageIndex = 1;
            await ReloadPageAsync();
        }

        private async void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_pageIndex <= 1) return;
            _pageIndex--;
            await ReloadPageAsync();
        }

        private async void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_pageIndex >= TotalPages) return;
            _pageIndex++;
            await ReloadPageAsync();
        }

        private async void LastPage_Click(object sender, RoutedEventArgs e)
        {
            _pageIndex = TotalPages;
            await ReloadPageAsync();
        }

        private async void PageSizeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _pageSize = PageSizeBox.SelectedIndex switch
            {
                0 => 10,
                1 => 20,
                2 => 50,
                3 => 100,
                _ => 50
            };
            _pageIndex = 1;

            if (IsInitialized && _currentTable != null)
                await ReloadPageAsync();
        }

        private async void PageNumberBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            if (!int.TryParse(PageNumberBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pageIndex))
            {
                PageNumberBox.Text = _pageIndex.ToString(CultureInfo.InvariantCulture);
                return;
            }

            _pageIndex = Math.Clamp(pageIndex, 1, TotalPages);
            await ReloadPageAsync();
        }

        private async Task ReloadPageAsync()
        {
            if (_currentProvider == null || _currentTable == null)
                return;

            CancelCurrentLoad();
            _loadCancellation = new CancellationTokenSource();
            var token = _loadCancellation.Token;

            try
            {
                await LoadPageAsync(token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _hasLoadedPage = false;
                StatusText.Text = string.Format(Properties.Resources.DB_QueryFailed, GetBaseMessage(ex));
                log.Error("查询数据库表失败", ex);
                PublishCopilotContext();
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void UpdatePagination()
        {
            if (_pageIndex > TotalPages) _pageIndex = TotalPages;
            if (_pageIndex < 1) _pageIndex = 1;

            PageNumberBox.Text = _pageIndex.ToString(CultureInfo.InvariantCulture);
            TotalPagesText.Text = TotalPages.ToString(CultureInfo.InvariantCulture);
            FirstPageButton.IsEnabled = _pageIndex > 1;
            PrevPageButton.IsEnabled = _pageIndex > 1;
            NextPageButton.IsEnabled = _pageIndex < TotalPages;
            LastPageButton.IsEnabled = _pageIndex < TotalPages;
        }

        private void UpdateWriteState()
        {
            var hasTable = _currentTable != null;
            var canWrite = hasTable && CanWriteCurrentTable;
            AddButton.IsEnabled = canWrite;
            SaveButton.IsEnabled = canWrite;
            RevertButton.IsEnabled = hasTable;
            DeleteButton.IsEnabled = canWrite && HasPrimaryKey;
            RefreshTableButton.IsEnabled = hasTable;
            RowsDataGrid.IsReadOnly = !canWrite || !HasPrimaryKey;
        }

        private void SetBusy(bool isBusy, string? statusText = null)
        {
            if (!string.IsNullOrWhiteSpace(statusText))
                StatusText.Text = statusText;

            BrowserTree.IsEnabled = !isBusy;
            SearchBox.IsEnabled = !isBusy;
            AddButton.IsEnabled = !isBusy && CanWriteCurrentTable;
            SaveButton.IsEnabled = !isBusy && CanWriteCurrentTable;
            DeleteButton.IsEnabled = !isBusy && CanWriteCurrentTable && HasPrimaryKey;
            RefreshTableButton.IsEnabled = !isBusy && _currentTable != null;
        }

        private void ClearTableView(string title, string source, string status)
        {
            CancelCurrentLoad();
            _currentProvider = null;
            _currentTable = null;
            _currentColumns = Array.Empty<DatabaseColumnInfo>();
            _currentDataTable = null;
            _totalCount = 0;
            _hasLoadedPage = false;
            RowsDataGrid.ItemsSource = null;
            PlaceholderText.Visibility = Visibility.Visible;
            TableTitleText.Text = title;
            TableSourceText.Text = source;
            TableMetaText.Text = string.Empty;
            RecordCountText.Text = string.Format(Properties.Resources.DB_RecordCount, "0");
            StatusText.Text = status;
            UpdatePagination();
            UpdateWriteState();
            PublishCopilotContext();
        }

        private async Task<CopilotDatabaseContextSnapshot?> CaptureCopilotDatabaseSnapshotAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Dispatcher.CheckAccess())
            {
                return await Dispatcher.InvokeAsync(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return CaptureCopilotDatabaseSnapshot();
                });
            }

            return CaptureCopilotDatabaseSnapshot();
        }

        private CopilotDatabaseContextSnapshot? CaptureCopilotDatabaseSnapshot()
        {
            if (_isDisposed)
                return null;

            var rows = _currentDataTable?.Rows.Cast<DataRow>().ToArray() ?? Array.Empty<DataRow>();
            var connectionState = _currentProvider == null
                ? "Browser open; no data source selected"
                : _hasLoadedPage
                    ? "Current page query succeeded"
                    : _currentColumns.Count > 0
                        ? "Schema query succeeded; page not loaded or refreshing"
                        : "Data source selected; connection not yet confirmed by a page query";
            return new CopilotDatabaseContextSnapshot
            {
                SourceId = CopilotDatabaseBrowserAgentExtension.SourceId,
                ConnectionState = connectionState,
                ProviderName = _currentProvider?.ProviderName ?? string.Empty,
                DatabaseType = _currentProvider?.DatabaseType.ToString() ?? string.Empty,
                DatabaseName = _currentTable?.DatabaseName ?? string.Empty,
                TableName = _currentTable?.TableName ?? string.Empty,
                TableComment = _currentTable?.Comment ?? string.Empty,
                Engine = _currentTable?.Engine ?? string.Empty,
                EstimatedRowCount = _currentTable?.RowCount,
                HasLoadedPage = _hasLoadedPage,
                QueryTotalCount = _totalCount,
                LoadedRowCount = _hasLoadedPage ? _currentDataTable?.Rows.Count ?? 0 : 0,
                PageIndex = _pageIndex,
                PageSize = _pageSize,
                TotalPages = TotalPages,
                SortColumn = _sortColumn ?? string.Empty,
                SortDirection = _sortDirection.ToString(),
                HasSearchFilter = !string.IsNullOrWhiteSpace(_searchKeyword),
                HasPrimaryKey = HasPrimaryKey,
                CanWrite = CanWriteCurrentTable,
                PendingAddedRows = rows.Count(row => row.RowState == DataRowState.Added),
                PendingModifiedRows = rows.Count(row => row.RowState == DataRowState.Modified),
                PendingDeletedRows = rows.Count(row => row.RowState == DataRowState.Deleted),
                Columns = _currentColumns.Select(column => new CopilotDatabaseColumnContextSnapshot
                {
                    ColumnName = column.ColumnName,
                    StoreType = column.StoreType,
                    Comment = column.Comment,
                    Ordinal = column.Ordinal,
                    IsNullable = column.IsNullable,
                    IsPrimaryKey = column.IsPrimaryKey,
                    IsIdentity = column.IsIdentity,
                    IsReadOnly = column.IsReadOnly,
                }).ToArray(),
            };
        }

        private void PublishCopilotContext()
        {
            if (_isDisposed || IsActive != true || _copilotContextSession?.IsCurrent != true)
                return;

            var snapshot = CaptureCopilotDatabaseSnapshot();
            if (snapshot == null)
                return;

            var item = CopilotBusinessContextBuilder.BuildDatabaseContextItem(snapshot);
            CopilotBusinessContextCoordinator.Publish(CopilotBusinessContextBundle.FromItem(
                CopilotDatabaseBrowserAgentExtension.SourceId,
                item));
        }

        private static Dictionary<string, object?> BuildValues(DataRow row, IReadOnlyList<DatabaseColumnInfo> columns)
        {
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in columns.Where(column => !column.IsIdentity && !column.IsReadOnly && !column.IsPrimaryKey))
            {
                if (!row.Table.Columns.Contains(column.ColumnName)) continue;
                values[column.ColumnName] = NormalizeValue(row[column.ColumnName, DataRowVersion.Current]);
            }

            foreach (var column in columns.Where(column => column.IsPrimaryKey && !column.IsIdentity && !column.IsReadOnly))
            {
                if (!row.Table.Columns.Contains(column.ColumnName)) continue;
                var value = NormalizeValue(row[column.ColumnName, DataRowVersion.Current]);
                if (value != null)
                    values[column.ColumnName] = value;
            }

            return values;
        }

        private static Dictionary<string, object?> BuildKeys(DataRow row, IReadOnlyList<DatabaseColumnInfo> columns, DataRowVersion version)
        {
            var keys = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in columns.Where(column => column.IsPrimaryKey))
            {
                if (!row.Table.Columns.Contains(column.ColumnName)) continue;
                var value = NormalizeValue(row[column.ColumnName, version]);
                if (value == null)
                    throw new InvalidOperationException($"主键 {column.ColumnName} 为空，不能定位行。以主键完整的表执行修改或删除。 ");

                keys[column.ColumnName] = value;
            }

            if (keys.Count == 0)
                throw new InvalidOperationException("当前表没有主键，不能定位行。");

            return keys;
        }

        private static object? NormalizeValue(object value)
        {
            return value == DBNull.Value ? null : value;
        }

        private static string GetBaseMessage(Exception ex)
        {
            return ex.GetBaseException().Message;
        }

        private void CancelCurrentLoad()
        {
            if (_loadCancellation == null) return;

            _loadCancellation.Cancel();
            _loadCancellation.Dispose();
            _loadCancellation = null;
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            var wasCurrentCopilotSession = _copilotContextSession?.IsCurrent == true;
            _isDisposed = true;
            CancelCurrentLoad();
            _copilotContextSession?.Dispose();
            _copilotContextSession = null;
            if (wasCurrentCopilotSession)
                CopilotLiveContextRegistry.Clear(CopilotDatabaseBrowserAgentExtension.SourceId);
            GC.SuppressFinalize(this);
        }
    }

    public enum DatabaseTreeNodeType
    {
        Provider,
        Catalog,
        Table,
        Loading,
        Error
    }

    public sealed class DatabaseTreeNode
    {
        private DatabaseTreeNode(DatabaseTreeNodeType nodeType)
        {
            NodeType = nodeType;
        }

        public DatabaseTreeNodeType NodeType { get; }
        public string Header { get; private set; } = string.Empty;
        public string Detail { get; private set; } = string.Empty;
        public IDatabaseBrowserProvider? Provider { get; private set; }
        public DatabaseCatalogInfo? Catalog { get; private set; }
        public DatabaseTableInfo? Table { get; private set; }
        public ObservableCollection<DatabaseTreeNode> Children { get; } = new();
        public bool IsLoaded { get; set; }

        public static DatabaseTreeNode CreateProvider(IDatabaseBrowserProvider provider)
        {
            var node = new DatabaseTreeNode(DatabaseTreeNodeType.Provider)
            {
                Header = provider.ProviderName,
                Detail = provider.DatabaseType == DatabaseType.Sqlite ? "SQLite" : "MySQL",
                Provider = provider
            };
            node.Children.Add(CreateLoading());
            return node;
        }

        public static DatabaseTreeNode CreateCatalog(IDatabaseBrowserProvider provider, DatabaseCatalogInfo catalog)
        {
            var node = new DatabaseTreeNode(DatabaseTreeNodeType.Catalog)
            {
                Header = string.IsNullOrWhiteSpace(catalog.DisplayName) ? catalog.Name : catalog.DisplayName,
                Detail = catalog.Name,
                Provider = provider,
                Catalog = catalog
            };
            node.Children.Add(CreateLoading());
            return node;
        }

        public static DatabaseTreeNode CreateTable(IDatabaseBrowserProvider provider, DatabaseCatalogInfo catalog, DatabaseTableInfo table)
        {
            var rowCount = table.RowCount.HasValue ? table.RowCount.Value.ToString("N0", CultureInfo.CurrentCulture) : string.Empty;
            return new DatabaseTreeNode(DatabaseTreeNodeType.Table)
            {
                Header = table.TableName,
                Detail = string.IsNullOrWhiteSpace(rowCount) ? table.Comment : rowCount,
                Provider = provider,
                Catalog = catalog,
                Table = table,
                IsLoaded = true
            };
        }

        public static DatabaseTreeNode CreateLoading()
        {
            return new DatabaseTreeNode(DatabaseTreeNodeType.Loading)
            {
                Header = Properties.Resources.DB_Loading,
                IsLoaded = true
            };
        }

        public static DatabaseTreeNode CreateError(string message)
        {
            return new DatabaseTreeNode(DatabaseTreeNodeType.Error)
            {
                Header = message,
                IsLoaded = true
            };
        }
    }
}
