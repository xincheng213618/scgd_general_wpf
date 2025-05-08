using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.Windows;


namespace ColorVision.Engine.Services
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
        public bool AutoRefreshView { get => _AutoRefreshView; set { _AutoRefreshView = value; NotifyPropertyChanged(); } }
        private bool _AutoRefreshView = true;
        public bool InsertAtBeginning { get => _InsertAtBeginning; set { _InsertAtBeginning = value; NotifyPropertyChanged(); } }
        private bool _InsertAtBeginning = true;
        public int SearchLimit { get => _SearchLimit; set { _SearchLimit = value; NotifyPropertyChanged(); } }
        private int _SearchLimit = 50;
    }
}
