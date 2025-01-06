using ColorVision.Common.MVVM;
using ColorVision.UI.PropertyEditor;
using System.ComponentModel;
using System.Windows;


namespace ColorVision.Engine.Services
{
    public abstract class ViewConfigBase : ViewModelBase
    {
        public RelayCommand EditCommand { get; set; }

        public ViewConfigBase()
        {
            EditCommand = new RelayCommand(a => new PropertyEditorWindow(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
        }

        [DisplayName("自动刷新数据")]
        public bool AutoRefreshView { get => _AutoRefreshView; set { _AutoRefreshView = value; NotifyPropertyChanged(); } }
        private bool _AutoRefreshView = true;
        [DisplayName("插入数据在列表前")]
        public bool InsertAtBeginning { get => _InsertAtBeginning; set { _InsertAtBeginning = value; NotifyPropertyChanged(); } }
        private bool _InsertAtBeginning = true;

        [DisplayName("搜索条数限制")]
        public int SearchLimit { get => _SearchLimit; set { _SearchLimit = value; NotifyPropertyChanged(); } }
        private int _SearchLimit = 50;
    }
}
