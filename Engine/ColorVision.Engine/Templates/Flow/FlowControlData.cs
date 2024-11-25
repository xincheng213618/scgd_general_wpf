using ColorVision.Common.MVVM;
using Newtonsoft.Json;

namespace ColorVision.Engine.Templates.Flow
{
    public class FlowControlData : ViewModelBase
    {
        public string Version { get => _Version; set { _Version = value; NotifyPropertyChanged(); } }
        private string _Version;
        public string ServiceName { get => _ServiceName; set { _ServiceName = value; NotifyPropertyChanged(); } }
        private string _ServiceName;

        public string EventName { get => _EventName; set { _EventName = value; NotifyPropertyChanged(); } }
        private string _EventName;

        public int ServiceID { get => _ServiceID; set { _ServiceID = value; NotifyPropertyChanged(); } }
        private int _ServiceID;

        public string SerialNumber { get => _SerialNumber; set { _SerialNumber = value; NotifyPropertyChanged(); } }
        private string _SerialNumber;

        public string MsgID { get => _MsgID; set { _MsgID = value; NotifyPropertyChanged(); } }
        private string _MsgID;

        [JsonProperty("params")]
        public dynamic Params { get => _Params; set { _Params = value; NotifyPropertyChanged(); } }
        private dynamic _Params;
    }
}
