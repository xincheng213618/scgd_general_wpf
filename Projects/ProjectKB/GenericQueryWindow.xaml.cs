using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql.ORM;
using ColorVision.UI;
using SqlSugar;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ProjectKB
{
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
        public SqlSugarClient Db { get; set; }

        public GenericQueryBase(SqlSugarClient db)
        {
            Db = db;
        }

        public virtual FrameworkElement GetControl()
        {
            throw new NotImplementedException();
        }

        public virtual void QueryDB()
        {

        }
    }

    public class GenericQuery<T> : GenericQueryBase where T : IPKModel, new()
    {
        public ISugarQueryable<T> Query { get; set; }
        public Collection<T> ViewResluts { get; set; }
        GenericQueryBaseConfig GenericQueryBaseConfig { get; set; }

        T QueryValue { get; set; }

        public GenericQuery(SqlSugarClient db, Collection<T> viewResluts) :base (db)
        {
            ViewResluts = viewResluts;
            GenericQueryBaseConfig = new GenericQueryBaseConfig();
            QueryValue = new T();
        }

        public override FrameworkElement GetControl()
        {
            StackPanel stackPanel = new StackPanel();
            stackPanel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(GenericQueryBaseConfig));

            // 反射遍历 QueryValue 属性，根据有效值拼接 Where
            foreach (var prop in typeof(T).GetProperties())
            {
                if (prop.PropertyType == typeof(string))
                {
                    prop.SetValue(QueryValue,string.Empty);
                }
                if (prop.PropertyType.IsEnum)
                {
                    var value = prop.GetValue(QueryValue);
                }
            }

            stackPanel.Children.Add(PropertyEditorHelper.GenPropertyEditorControl(QueryValue));

            return stackPanel;
        }

        public override void QueryDB()
        {
            ViewResluts.Clear();
            var query = Db.Queryable<T>();

            // 反射遍历 QueryValue 属性，根据有效值拼接 Where
            foreach (var prop in typeof(T).GetProperties())
            {
                var value = prop.GetValue(QueryValue);
                if (value == null) continue;
                string propName = prop.Name;
                var SugarColumn = prop.GetCustomAttribute<SugarColumn>();
                if  (SugarColumn != null)
                {
                    if (SugarColumn.IsIgnore) continue;
                    if (SugarColumn.ColumnName !=null) propName = SugarColumn.ColumnName;
                }
                var Browsable = prop.GetCustomAttribute<BrowsableAttribute>();
                if (Browsable != null && Browsable.Browsable == false) continue;
                 
                if (prop.PropertyType == typeof(string))
                {
                    string strValue = (string)value;
                    if (!string.IsNullOrWhiteSpace(strValue))
                    {
                        var param = new Dictionary<string, object>();
                        param[propName] = $"%{strValue}%";
                        query = query.Where($"{propName} LIKE @{propName}", param);
                    }
                }
                else if (prop.PropertyType.IsEnum)
                {
                    var param = new Dictionary<string, object>();
                    param[propName] = (int)value; // 强制转int比较保险
                    query = query.Where($"{propName} == @{propName}", param);
                }
            }
            
            query = query.OrderBy(x => x.Id, GenericQueryBaseConfig.OrderByType);

            var dbList = GenericQueryBaseConfig.Count > 0 ? query.Take(GenericQueryBaseConfig.Count).ToList() : query.ToList();

            foreach (var dbItem in dbList)
            {
                ViewResluts.Add(dbItem);
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
            QueryGrid.Children.Add(GenericQueryBase.GetControl());
        }

        private void Query_Click(object sender, RoutedEventArgs e)
        {
            GenericQueryBase.QueryDB();
        }
    }
}
