using ColorVision.Common.MVVM;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.Jsons.LargeFlow 
{ 

    public class LargeFlowConfig : ViewModelBase
    {
        public ObservableCollection<string> Flows { get => _Flows; set { _Flows = value; OnPropertyChanged(); } }

        private ObservableCollection<string> _Flows = new ObservableCollection<string>();

        public string ReceiptName { get => _ReceiptName; set { _ReceiptName = value; OnPropertyChanged(); } }
        private string _ReceiptName;

        public string ReceiptConfig { get => _ReceiptConfig; set { _ReceiptConfig = value; OnPropertyChanged(); } }
        private string _ReceiptConfig;

    }
}
