using ColorVision.Common.MVVM;
using ColorVision.UI;
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

        public bool AutoRefreshView { get => _AutoRefreshView; set { _AutoRefreshView = value; NotifyPropertyChanged(); } }
        private bool _AutoRefreshView = true;
        public bool InsertAtBeginning { get => _InsertAtBeginning; set { _InsertAtBeginning = value; NotifyPropertyChanged(); } }
        private bool _InsertAtBeginning = true;
        public int SearchLimit { get => _SearchLimit; set { _SearchLimit = value; NotifyPropertyChanged(); } }
        private int _SearchLimit = 50;
    }
}
