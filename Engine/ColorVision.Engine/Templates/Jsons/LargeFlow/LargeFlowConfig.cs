using ColorVision.Common.MVVM;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.Jsons.LargeFlow 
{ 

    public class LargeFlowConfig : ViewModelBase
    {
        public ObservableCollection<string> Flows { get => _Flows; set { _Flows = value; NotifyPropertyChanged(); } }

        private ObservableCollection<string> _Flows = new ObservableCollection<string>();

        public string ReceiptName { get => _ReceiptName; set { _ReceiptName = value; NotifyPropertyChanged(); } }
        private string _ReceiptName;

        public string ReceiptConfig { get => _ReceiptConfig; set { _ReceiptConfig = value; NotifyPropertyChanged(); } }
        private string _ReceiptConfig;

    }
}
