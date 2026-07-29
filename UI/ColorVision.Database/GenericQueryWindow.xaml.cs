#pragma warning disable CA1725,CA1863,CS8604
using ColorVision.Common.MVVM;
using ColorVision.Database.Properties;
using ColorVision.Themes;
using log4net;
using SqlSugar;
using System.ComponentModel.DataAnnotations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ColorVision.Database
{
    public enum QueryOperator
    {
        [Description("=")]
        Equal,      // =
        [Description("<>")]
        NotEqual,   // <>
        [Description(">")]
        Greater,    // >
        [Description("<")]
        Less,       // <
        [Description(">=")]
        GreaterOrEqual, // >=
        [Description("<=")]
        LessOrEqual,    // <=
        [Description("LIKE")]
        Like        // LIKE
    }


    public class QueryCondition
    {
        public PropertyInfo Property { get; set; } = null!;
        public QueryOperator Operator { get; set; } // "=", ">", "<", ">=", "<=", "LIKE"
        public object? Value { get; set; }
        public string? InputText { get; set; }
        public FrameworkElement? UiRow { get; set; }
        public Control? ValueEditor { get; set; }
        public TextBlock? ErrorText { get; set; }
    }

    public class GenericQueryBaseConfig:ViewModelBase
    {
        [Display(Name = "DB_QueryCount", ResourceType = typeof(Properties.Resources)), Category("View")]
        public int Count { get => _Count; set { _Count = value; OnPropertyChanged(); } }
        private int _Count = 100;

        [Display(Name = "DB_SortByType", ResourceType = typeof(Properties.Resources)), Category("View")]
        public OrderByType OrderByType { get => _OrderByType; set { _OrderByType = value; OnPropertyChanged(); } }
        private OrderByType _OrderByType = OrderByType.Desc;
    }
    public class QueryCompletedEventArgs : EventArgs
    {
        public int ResultCount { get; init; }
        public TimeSpan Elapsed { get; init; }
        public string Sql { get; init; } = string.Empty;
    }

    public class GenericQueryBase:ViewModelBase
    {
        public static readonly ILog log = LogManager.GetLogger(typeof(GenericQueryBase));
        public SqlSugarClient Db { get; }
        public ObservableCollection<KeyValuePair<string, PropertyInfo>> PropertyInfos { get; protected set; } = new();
        public GenericQueryBaseConfig QueryConfig { get; } = new();
        public int ConditionCount { get; private set; }
        public Control? LastConditionEditor { get; protected set; }
        public string Sql { get => _Sql; set { _Sql = value; OnPropertyChanged(); } }
        private string _Sql = string.Empty;

        public RelayCommand DeleteAllCommand { get; }
        public RelayCommand TruncateTableCommand { get; }
        public event EventHandler? PreQuery;
        public event EventHandler<QueryCompletedEventArgs>? QueryCompleted;
        public event EventHandler? ConditionsChanged;


        public GenericQueryBase(SqlSugarClient db)
        {
            Db = db;
            DeleteAllCommand = new RelayCommand(_ => DeleteAll());
            TruncateTableCommand = new RelayCommand(_ => TruncateTable());
        }
        protected virtual void OnPreQuery() => PreQuery?.Invoke(this, EventArgs.Empty);
        protected virtual void OnQueryCompleted(QueryCompletedEventArgs e) => QueryCompleted?.Invoke(this, e);
        protected void OnConditionsChanged(int conditionCount)
        {
            ConditionCount = conditionCount;
            ConditionsChanged?.Invoke(this, EventArgs.Empty);
        }

        public virtual FrameworkElement GetControl() => throw new NotImplementedException();
        public virtual void AddPropertyInfo(PropertyInfo propertyInfo) => throw new NotImplementedException();
        public virtual void RemoveCondition(QueryCondition condition) { }
        public virtual void ResetConditions() { }
        public virtual void AddAllPropertyInfos() { }
        public virtual void QueryDB() => OnPreQuery();

        public virtual void DeleteAll() { }
        public virtual void TruncateTable() { }
    }


    public class GenericQuery<T> : GenericQueryBase where T : class ,IEntity,new()
    {
        public ISugarQueryable<T> Query { get; set; } = null!;
        public IList<T> ViewResluts { get; set; }
        internal ObservableCollection<QueryCondition> QueryConditions { get; } = new();

        public GenericQuery(SqlSugarClient db, IList<T> viewResluts) : base(db)
        {
            ViewResluts = viewResluts;
            PropertyInfos = new ObservableCollection<KeyValuePair<string, PropertyInfo>>(
                GenericQueryConditionSupport.GetQueryableProperties(typeof(T)));
        }
        public StackPanel QueryStackPanel { get; set; } = new StackPanel();

        public override FrameworkElement GetControl()
        {
            QueryStackPanel = new StackPanel();
            return QueryStackPanel;
        }

        public override void AddPropertyInfo(PropertyInfo property)
        {
            var queryCondition = new QueryCondition { Property = property };
            QueryStackPanel.Children.Add(GenericQueryConditionSupport.CreateConditionRow(queryCondition, RemoveCondition_Click));
            QueryConditions.Add(queryCondition);
            LastConditionEditor = queryCondition.ValueEditor;
            OnConditionsChanged(QueryConditions.Count);
        }

        private void RemoveCondition_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is QueryCondition condition)
            {
                RemoveCondition(condition);
            }
        }

        public override void RemoveCondition(QueryCondition condition)
        {
            if (condition.UiRow != null)
                QueryStackPanel.Children.Remove(condition.UiRow);
            QueryConditions.Remove(condition);
            LastConditionEditor = QueryConditions.LastOrDefault()?.ValueEditor;
            OnConditionsChanged(QueryConditions.Count);
        }

        public override void ResetConditions()
        {
            QueryStackPanel.Children.Clear();
            QueryConditions.Clear();
            LastConditionEditor = null;
            OnConditionsChanged(QueryConditions.Count);
        }

        public override void AddAllPropertyInfos()
        {
            foreach (var kvp in PropertyInfos)
                AddPropertyInfo(kvp.Value);
        }

        public override void QueryDB()
        {
            base.QueryDB();
            Stopwatch _stopwatch = Stopwatch.StartNew();

            var query = GenericQueryConditionSupport.ApplyConditions(Db.Queryable<T>(), QueryConditions);
            query = query.OrderBy(x => x.Id, QueryConfig.OrderByType);

            Sql = query.ToSqlString(); // 触发SQL生成
            log.InfoFormat("GenericQuery SQL: {0}", Sql);
            var dbList = QueryConfig.Count > 0 ? query.Take(QueryConfig.Count).ToList() : query.ToList();

            ViewResluts.Clear();
            foreach (var dbItem in dbList)
            {
                ViewResluts.Add(dbItem);
            }


            _stopwatch.Stop();
            OnQueryCompleted(new QueryCompletedEventArgs() { Sql = Sql, ResultCount = dbList.Count, Elapsed = _stopwatch.Elapsed });
        }

        /// <summary>
        /// 清空表数据（Delete All Rows, 保留表结构，自增不重置）
        /// </summary>
        public override void DeleteAll()
        {
            var tableName = Db.EntityMaintenance.GetTableName<T>();
            Db.Deleteable<T>().ExecuteCommand();
            log.InfoFormat("Delete all rows from {0}", tableName);
        }

        /// <summary>
        /// 截断表（Truncate Table，删除所有数据且重置自增主键）
        /// </summary>
        public override void TruncateTable()
        {
            var tableName = Db.EntityMaintenance.GetTableName<T>();
            var sql = $"TRUNCATE TABLE {tableName}";
            Db.Ado.ExecuteCommand(sql);
            log.InfoFormat("Truncate table {0}", tableName);
        }

    }

    public class GenericQuery<T,T1> : GenericQueryBase where T :class, IEntity, new() where T1 : new()
    {
        public ISugarQueryable<T> Query { get; set; } = null!;
        public IList<T1> ViewResluts { get; set; }
        internal ObservableCollection<QueryCondition> QueryConditions { get; } = new();
        Func<T, T1> Converter { get; set; }

        public GenericQuery(SqlSugarClient db, IList<T1> viewResluts,Func<T, T1> converter) :base (db)
        {
            ViewResluts = viewResluts;
            Converter = converter;
            PropertyInfos = new ObservableCollection<KeyValuePair<string, PropertyInfo>>(
                GenericQueryConditionSupport.GetQueryableProperties(typeof(T)));
        }
        public StackPanel QueryStackPanel { get; set; } = new StackPanel();

        public override FrameworkElement GetControl()
        {
            QueryStackPanel = new StackPanel();
            return QueryStackPanel;
        }

        public override void AddPropertyInfo(PropertyInfo property)
        {
            var queryCondition = new QueryCondition { Property = property };
            QueryStackPanel.Children.Add(GenericQueryConditionSupport.CreateConditionRow(queryCondition, RemoveCondition_Click));
            QueryConditions.Add(queryCondition);
            LastConditionEditor = queryCondition.ValueEditor;
            OnConditionsChanged(QueryConditions.Count);
        }

        private void RemoveCondition_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is QueryCondition condition)
            {
                RemoveCondition(condition);
            }
        }

        public override void RemoveCondition(QueryCondition condition)
        {
            if (condition.UiRow != null)
                QueryStackPanel.Children.Remove(condition.UiRow);
            QueryConditions.Remove(condition);
            LastConditionEditor = QueryConditions.LastOrDefault()?.ValueEditor;
            OnConditionsChanged(QueryConditions.Count);
        }

        public override void ResetConditions()
        {
            QueryStackPanel.Children.Clear();
            QueryConditions.Clear();
            LastConditionEditor = null;
            OnConditionsChanged(QueryConditions.Count);
        }

        public override void AddAllPropertyInfos()
        {
            foreach (var kvp in PropertyInfos)
                AddPropertyInfo(kvp.Value);
        }

        public override void QueryDB()
        {
            base.QueryDB();
            Stopwatch _stopwatch = Stopwatch.StartNew();


            var query = GenericQueryConditionSupport.ApplyConditions(Db.Queryable<T>(), QueryConditions);
            query = query.OrderBy(x => x.Id, QueryConfig.OrderByType);
            Sql = query.ToSqlString(); // 触发SQL生成
            log.InfoFormat("GenericQuery SQL: {0}", Sql);
            var dbList = QueryConfig.Count > 0 ? query.Take(QueryConfig.Count).ToList() : query.ToList();

            ViewResluts.Clear();
            foreach (var dbItem in dbList)
            {
                ViewResluts.Add(Converter(dbItem));
            }

            _stopwatch.Stop();
            OnQueryCompleted(new QueryCompletedEventArgs() { Sql =Sql,ResultCount = dbList.Count,Elapsed = _stopwatch.Elapsed });
        }


        /// <summary>
        /// 清空表数据（Delete All Rows, 保留表结构，自增不重置）
        /// </summary>
        public override void DeleteAll()
        {
            var tableName = Db.EntityMaintenance.GetTableName<T>();
            Db.Deleteable<T>().ExecuteCommand();
            log.InfoFormat("Delete all rows from {0}", tableName);
        }

        /// <summary>
        /// 截断表（Truncate Table，删除所有数据且重置自增主键）
        /// </summary>
        public override void TruncateTable()
        {
            var tableName = Db.EntityMaintenance.GetTableName<T>();
            var sql = $"TRUNCATE TABLE {tableName}";
            Db.Ado.ExecuteCommand(sql);
            log.InfoFormat("Truncate table {0}", tableName);
        }

    }


    /// <summary>
    /// GenericQueryWindow.xaml 的交互逻辑
    /// </summary>
    public partial class GenericQueryWindow : Window
    {
        public GenericQueryBase GenericQueryBase { get; set; }
        public GenericQueryWindow(GenericQueryBase genericQueryBase)
        {
            GenericQueryBase = genericQueryBase;
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = GenericQueryBase;
            PropertyInfoCB.ItemsSource = GenericQueryBase.PropertyInfos;
            PropertyInfoCB.SelectedIndex = -1;
            QueryGrid.Children.Add(GenericQueryBase.GetControl());
            MaxResultsTextBox.Text = GenericQueryBase.QueryConfig.Count.ToString(CultureInfo.CurrentCulture);
            SortDirectionCB.SelectedIndex = GenericQueryBase.QueryConfig.OrderByType == OrderByType.Desc ? 0 : 1;

            GenericQueryBase.ConditionsChanged += (_, _) => UpdateConditionState();
            UpdateConditionState();
            Dispatcher.BeginInvoke(PropertyInfoCB.Focus, DispatcherPriority.Input);
        }

        private async void Query_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(MaxResultsTextBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var maxResults)
                || maxResults is < 1 or > 10000)
            {
                StatusText.Text = Properties.Resources.DB_MaxResultsRange;
                MaxResultsTextBox.Focus();
                MaxResultsTextBox.SelectAll();
                return;
            }

            GenericQueryBase.QueryConfig.Count = maxResults;
            GenericQueryBase.QueryConfig.OrderByType = SortDirectionCB.SelectedIndex == 1 ? OrderByType.Asc : OrderByType.Desc;
            StatusText.Text = Properties.Resources.DB_Querying;
            ApplyFilterButton.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                await Dispatcher.Yield(DispatcherPriority.Background);
                GenericQueryBase.QueryDB();
                DialogResult = true;
            }
            catch (Exception ex)
            {
                StatusText.Text = string.Format(Properties.Resources.DB_QueryFailed, ex.Message);
            }
            finally
            {
                Mouse.OverrideCursor = null;
                ApplyFilterButton.IsEnabled = true;
            }
        }

        private void AddPropertyInfo_Click(object sender, RoutedEventArgs e)
        {
            if (PropertyInfoCB.SelectedValue is PropertyInfo property)
            {
                GenericQueryBase.AddPropertyInfo(property);
                PropertyInfoCB.SelectedIndex = -1;
                StatusText.Text = Properties.Resources.DB_Ready;
                GenericQueryBase.LastConditionEditor?.Focus();
            }
        }

        private void PropertyInfoCB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AddConditionButton.IsEnabled = PropertyInfoCB.SelectedValue is PropertyInfo;
        }

        private void PropertyInfoCB_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || PropertyInfoCB.SelectedValue is not PropertyInfo)
                return;

            AddPropertyInfo_Click(AddConditionButton, new RoutedEventArgs());
            e.Handled = true;
        }

        private void ResetConditions_Click(object sender, RoutedEventArgs e)
        {
            GenericQueryBase.ResetConditions();
            StatusText.Text = Properties.Resources.DB_ResetDone;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void UpdateConditionState()
        {
            var hasConditions = GenericQueryBase.ConditionCount > 0;
            EmptyStatePanel.Visibility = hasConditions ? Visibility.Collapsed : Visibility.Visible;
            ResetConditionsButton.Visibility = hasConditions ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
