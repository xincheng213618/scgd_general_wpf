using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using ColorVision.UI.PropertyEditor;
using ColorVision.UI.Sorts;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.SMU.Views
{
    public class ViewSMUConfig :ViewModelBase, IConfig
    {
        public static ViewSMUConfig Instance => ConfigService.Instance.GetRequiredService<ViewSMUConfig>();

        public RelayCommand EditCommand { get; set; }

        public ViewSMUConfig()
        {
            EditCommand = new RelayCommand(a => new PropertyEditorWindow(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
        }

        [DisplayName("自动刷新数据")]
        public bool AutoRefreshView { get => _AutoRefreshView; set { _AutoRefreshView = value; NotifyPropertyChanged(); } }
        private bool _AutoRefreshView = true;
        [DisplayName("插入数据在列表前")]
        public bool InsertAtBeginning { get => _InsertAtBeginning; set { _InsertAtBeginning = value; NotifyPropertyChanged(); } }
        private bool _InsertAtBeginning = true;


        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

    }
}
