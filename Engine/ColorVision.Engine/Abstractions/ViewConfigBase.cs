using ColorVision.Common.MVVM;
using ColorVision.UI;
using SqlSugar;
using System.ComponentModel;
using System.Windows;


namespace ColorVision.Engine
{
    /// <summary>
    /// 配置视图的基类，提供通用配置选项
    /// </summary>
    public abstract class ViewConfigBase : ViewModelBase
    {
        /// <summary>
         /// 获取用于编辑属性的命令
         /// </summary>
        public RelayCommand EditCommand { get; set; }

        public ViewConfigBase()
        {
            EditCommand = new RelayCommand(a => new PropertyEditorWindow(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
        }

        /// <summary>
        /// 指示视图是否应自动刷新
        /// </summary>
        public bool AutoRefreshView { get => _AutoRefreshView; set { _AutoRefreshView = value; OnPropertyChanged(); } }
        private bool _AutoRefreshView = true;
        public bool InsertAtBeginning { get => _InsertAtBeginning; set { _InsertAtBeginning = value; OnPropertyChanged(); } }
        private bool _InsertAtBeginning = true;

        [DisplayName("查询数量"), Category("View")]
        public int Count { get => _Count; set { _Count = value; OnPropertyChanged(); } }
        private int _Count = 50;
        [DisplayName("按类型排序"), Category("View")]
        public OrderByType OrderByType { get => _OrderByType; set { _OrderByType = value; OnPropertyChanged(); } }
        private OrderByType _OrderByType = OrderByType.Desc;


    }
}
