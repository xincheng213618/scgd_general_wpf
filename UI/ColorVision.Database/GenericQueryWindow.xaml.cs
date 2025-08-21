using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql.ORM;
using ColorVision.UI;
using log4net;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Database
{
    public enum QueryOperator
    {
        [Description("=")]
        Equal,      // =
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
        public PropertyInfo Property { get; set; }
        public QueryOperator Operator { get; set; } // "=", ">", "<", ">=", "<=", "LIKE"
        public object Value { get; set; }
    }

    public class GenericQueryBaseConfig:ViewModelBase
    {
        [DisplayName("查询数量"), Category("View")]
        public int Count { get => _Count; set { _Count = value; NotifyPropertyChanged(); } }
        private int _Count = -1;

        [DisplayName("按类型排序"), Category("View")]
        public OrderByType OrderByType { get => _OrderByType; set { _OrderByType = value; NotifyPropertyChanged(); } }
        private OrderByType _OrderByType = OrderByType.Desc;
    }

    public class GenericQueryBase:ViewModelBase
    {
        public static readonly ILog log = LogManager.GetLogger(typeof(GenericQueryBase));

        public SqlSugarClient Db { get; set; }

        public ObservableCollection<KeyValuePair<string, PropertyInfo>> PropertyInfos { get; set; }

        public string Sql { get => _Sql; set { _Sql = value;NotifyPropertyChanged(); } }
        private string _Sql;

        public GenericQueryBase(SqlSugarClient db)
        {
            Db = db;
        }

        public virtual FrameworkElement GetControl()
        {
            throw new NotImplementedException();
        }

        public virtual void AddPropertyInfo(PropertyInfo propertyInfo)
        {
            throw new NotImplementedException();
        }

        public virtual void QueryDB()
        {

        }
    }


    public class GenericQuery<T> : GenericQueryBase where T : IPKModel,new()
    {
        public ISugarQueryable<T> Query { get; set; }
        public IList<T> ViewResluts { get; set; }
        GenericQueryBaseConfig GenericQueryBaseConfig { get; set; }

        T QueryValue { get; set; }
        ObservableCollection<QueryCondition> QueryConditions { get; set; }

        public GenericQuery(SqlSugarClient db, IList<T> viewResluts) : base(db)
        {
            ViewResluts = viewResluts;
            GenericQueryBaseConfig = new GenericQueryBaseConfig();
            QueryValue = new T();
            PropertyInfos = new ObservableCollection<KeyValuePair<string, PropertyInfo>>();
            QueryConditions = new ObservableCollection<QueryCondition>();

            foreach (var prop in typeof(T).GetProperties())
            {
                string propName = prop.Name;
                var SugarColumn = prop.GetCustomAttribute<SugarColumn>();
                if (SugarColumn != null)
                {
                    if (SugarColumn.IsIgnore) continue;
                    if (SugarColumn.ColumnName != null) propName = SugarColumn.ColumnName;
                }
                var Browsable = prop.GetCustomAttribute<BrowsableAttribute>();
                if (Browsable != null && Browsable.Browsable == false) continue;
                PropertyInfos.Add(new KeyValuePair<string, PropertyInfo>(propName, prop));
            }
        }
        public StackPanel QueryStackPanel { get; set; } = new StackPanel();

        public override FrameworkElement GetControl()
        {
            StackPanel stackPanel = new StackPanel();
            stackPanel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(GenericQueryBaseConfig));
            var border = new Border
            {
                Background = (Brush)Application.Current.FindResource("GlobalBorderBrush"),
                BorderThickness = new Thickness(1),
                BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(0, 0, 0, 5)
            };
            QueryStackPanel = new StackPanel { Margin = new Thickness(5, 5, 5, 0) };
            border.Child = QueryStackPanel;
            stackPanel.Children.Add(border);
            return stackPanel;
        }
        public override void AddPropertyInfo(PropertyInfo property)
        {
            PropertyInfos.Remove(PropertyInfos.First(a => a.Value == property));
            QueryCondition queryCondition = new QueryCondition() { Property =property };
            DockPanel dockPanel = new DockPanel();
            if (property.PropertyType == typeof(bool))
            {
                dockPanel = PropertyEditorHelper.GenBoolProperties(property, QueryValue);
            }
            else if (property.PropertyType == typeof(int) || property.PropertyType == (typeof(float)) || property.PropertyType == (typeof(Rect)) || property.PropertyType == typeof(uint) || property.PropertyType == typeof(long) || property.PropertyType == typeof(ulong) || property.PropertyType == typeof(sbyte) || property.PropertyType == typeof(double) || property.PropertyType == typeof(string))
            {
                dockPanel = PropertyEditorHelper.GenTextboxProperties(property, QueryValue);
            }
            else if (property.PropertyType.IsEnum)
            {
                dockPanel = PropertyEditorHelper.GenEnumProperties(property, QueryValue);
            }
            PropertyInfo propertyInfo = typeof(QueryCondition).GetProperty("Operator");
            dockPanel.Children.Insert(0,PropertyEditorHelper.GenEnumPropertiesComboBox(propertyInfo, queryCondition));
            dockPanel.Margin = new Thickness(0, 0, 0, 5);
            QueryStackPanel.Children.Add(dockPanel);
            QueryConditions.Add(queryCondition);
        }
        public override void QueryDB()
        {
            ViewResluts.Clear();
            var query = Db.Queryable<T>();

            // 反射遍历 QueryValue 属性，根据有效值拼接 Where
            foreach (var prop in QueryConditions)
            {
                var value = prop.Property.GetValue(QueryValue);
                if (value == null) continue;
                string propName = prop.Property.Name;
                var SugarColumn = prop.Property.GetCustomAttribute<SugarColumn>();
                if (SugarColumn != null)
                {
                    if (SugarColumn.IsIgnore) continue;
                    if (SugarColumn.ColumnName != null) propName = SugarColumn.ColumnName;
                }
                var Browsable = prop.Property.GetCustomAttribute<BrowsableAttribute>();
                if (Browsable != null && Browsable.Browsable == false) continue;
                if (prop.Property.PropertyType.IsEnum)
                {
                    var param = new Dictionary<string, object>();
                    param[propName] = (int)value; // 强制转int比较保险
                    query = query.Where($"{propName} {prop.Operator.ToDescription()} @{propName}", param);
                }
                else if (prop.Property.PropertyType == typeof(int)|| prop.Property.PropertyType == typeof(int?)|| prop.Property.PropertyType == typeof(double)|| prop.Property.PropertyType == typeof(double?))
                {
                    var param = new Dictionary<string, object>();
                    param[propName] = value;
                    query = query.Where($"{propName} {prop.Operator.ToDescription()} @{propName}", param);
                }
                else if (prop.Property.PropertyType == typeof(string))
                {
                    string strValue = (string)value;
                    if (!string.IsNullOrWhiteSpace(strValue))
                    {
                        var param = new Dictionary<string, object>();
                        param[propName] = $"%{strValue}%";
                        query = query.Where($"{propName} {prop.Operator.ToDescription()} @{propName}", param);
                    }
                }

            }

            query = query.OrderBy(x => x.Id, GenericQueryBaseConfig.OrderByType);

            Sql = query.ToSqlString(); // 触发SQL生成
            log.InfoFormat("GenericQuery SQL: {0}", Sql);
            var dbList = GenericQueryBaseConfig.Count > 0 ? query.Take(GenericQueryBaseConfig.Count).ToList() : query.ToList();

            foreach (var dbItem in dbList)
            {
                ViewResluts.Add(dbItem);
            }
        }

    }

    public class GenericQuery<T,T1> : GenericQueryBase where T : IPKModel, new() where T1 : new()
    {
        public ISugarQueryable<T> Query { get; set; }
        public IList<T1> ViewResluts { get; set; }
        GenericQueryBaseConfig GenericQueryBaseConfig { get; set; }

        T QueryValue { get; set; }
        ObservableCollection<QueryCondition> QueryConditions { get; set; }
        Func<T, T1> Converter { get; set; }

        public GenericQuery(SqlSugarClient db, IList<T1> viewResluts,Func<T, T1> converter) :base (db)
        {
            ViewResluts = viewResluts;
            GenericQueryBaseConfig = new GenericQueryBaseConfig();
            Converter = converter;
            QueryValue = new T();
            PropertyInfos = new ObservableCollection<KeyValuePair<string, PropertyInfo>>();
            QueryConditions = new ObservableCollection<QueryCondition>();

            foreach (var prop in typeof(T).GetProperties())
            {
                string propName = prop.Name;
                var SugarColumn = prop.GetCustomAttribute<SugarColumn>();
                if (SugarColumn != null)
                {
                    if (SugarColumn.IsIgnore) continue;
                    if (SugarColumn.ColumnName != null) propName = SugarColumn.ColumnName;
                }
                var Browsable = prop.GetCustomAttribute<BrowsableAttribute>();
                if (Browsable != null && Browsable.Browsable == false) continue;
                PropertyInfos.Add(new KeyValuePair<string, PropertyInfo>(propName, prop));
            }
        }
        public StackPanel QueryStackPanel { get; set; } = new StackPanel();

        public override FrameworkElement GetControl()
        {
            StackPanel stackPanel = new StackPanel();
            stackPanel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(GenericQueryBaseConfig));
            var border = new Border
            {
                Background = (Brush)Application.Current.FindResource("GlobalBorderBrush"),
                BorderThickness = new Thickness(1),
                BorderBrush = (Brush)Application.Current.FindResource("BorderBrush"),
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(0, 0, 0, 5)
            };
            QueryStackPanel = new StackPanel { Margin = new Thickness(5, 5, 5, 0) };
            border.Child = QueryStackPanel;
            stackPanel.Children.Add(border);
            return stackPanel;
        }
        public override void AddPropertyInfo(PropertyInfo property)
        {
            PropertyInfos.Remove(PropertyInfos.First(a => a.Value == property));
            QueryCondition queryCondition = new QueryCondition() { Property = property };
            DockPanel dockPanel = new DockPanel();
            if (property.PropertyType == typeof(bool))
            {
                dockPanel = PropertyEditorHelper.GenBoolProperties(property, QueryValue);
            }
            else if (property.PropertyType == typeof(int) || property.PropertyType == (typeof(float)) || property.PropertyType == (typeof(Rect)) || property.PropertyType == typeof(uint) || property.PropertyType == typeof(long) || property.PropertyType == typeof(ulong) || property.PropertyType == typeof(sbyte) || property.PropertyType == typeof(double) || property.PropertyType == typeof(string))
            {
                dockPanel = PropertyEditorHelper.GenTextboxProperties(property, QueryValue);
            }
            else if (property.PropertyType.IsEnum)
            {
                dockPanel = PropertyEditorHelper.GenEnumProperties(property, QueryValue);
            }
            PropertyInfo propertyInfo = typeof(QueryCondition).GetProperty("Operator");
            dockPanel.Children.Insert(0, PropertyEditorHelper.GenEnumPropertiesComboBox(propertyInfo, queryCondition));
            dockPanel.Margin = new Thickness(0, 0, 0, 5);
            QueryStackPanel.Children.Add(dockPanel);
            QueryConditions.Add(queryCondition);
        }
        public override void QueryDB()
        {
            ViewResluts.Clear();
            var query = Db.Queryable<T>();

            // 反射遍历 QueryValue 属性，根据有效值拼接 Where
            foreach (var prop in QueryConditions)
            {
                var value = prop.Property.GetValue(QueryValue);
                if (value == null) continue;
                string propName = prop.Property.Name;
                var SugarColumn = prop.Property.GetCustomAttribute<SugarColumn>();
                if (SugarColumn != null)
                {
                    if (SugarColumn.IsIgnore) continue;
                    if (SugarColumn.ColumnName != null) propName = SugarColumn.ColumnName;
                }
                var Browsable = prop.Property.GetCustomAttribute<BrowsableAttribute>();
                if (Browsable != null && Browsable.Browsable == false) continue;

                if (prop.Property.PropertyType.IsEnum)
                {
                    var param = new Dictionary<string, object>();
                    param[propName] = (int)value; // 强制转int比较保险
                    query = query.Where($"{propName} {prop.Operator.ToDescription()} @{propName}", param);
                }
                else if (prop.Property.PropertyType == typeof(int) || prop.Property.PropertyType == typeof(int?) || prop.Property.PropertyType == typeof(double) || prop.Property.PropertyType == typeof(double?))
                {
                    var param = new Dictionary<string, object>();
                    param[propName] = value;
                    query = query.Where($"{propName} {prop.Operator.ToDescription()} @{propName}", param);
                }
                else if (prop.Property.PropertyType == typeof(string))
                {
                    string strValue = (string)value;
                    if (!string.IsNullOrWhiteSpace(strValue))
                    {
                        var param = new Dictionary<string, object>();
                        param[propName] = $"%{strValue}%";
                        query = query.Where($"{propName} {prop.Operator.ToDescription()} @{propName}", param);
                    }
                }
            }

            query = query.OrderBy(x => x.Id, GenericQueryBaseConfig.OrderByType);
            Sql = query.ToSqlString(); // 触发SQL生成
            log.InfoFormat("GenericQuery SQL: {0}", Sql);
            var dbList = GenericQueryBaseConfig.Count > 0 ? query.Take(GenericQueryBaseConfig.Count).ToList() : query.ToList();

            foreach (var dbItem in dbList)
            {
                ViewResluts.Add(Converter(dbItem));
            }
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
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = GenericQueryBase;
            PropertyInfoCB.ItemsSource = GenericQueryBase.PropertyInfos;
            QueryGrid.Children.Add(GenericQueryBase.GetControl());
        }

        private void Query_Click(object sender, RoutedEventArgs e)
        {
            GenericQueryBase.QueryDB();
        }

        private void AddPropertyInfo_Click(object sender, RoutedEventArgs e)
        {
            if (PropertyInfoCB.SelectedValue is PropertyInfo  property)
            {
                GenericQueryBase.AddPropertyInfo(property);
            }
        }
    }
}
