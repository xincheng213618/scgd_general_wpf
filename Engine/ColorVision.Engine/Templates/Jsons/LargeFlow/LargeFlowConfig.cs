using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.POI;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.Jsons.LargeFlow 
{ 

    public class LargeFlowConfig : ViewModelBase
    {
        public ObservableCollection<string> Flows { get => _Flows; set { _Flows = value; NotifyPropertyChanged(); } }
        private ObservableCollection<string> _Flows = new ObservableCollection<string>();
    }
}
