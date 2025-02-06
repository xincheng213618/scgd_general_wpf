using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.Camera.Video
{
    public class CameraVideoConfig : ViewModelBase
    {
        [DisplayName("主机")]
        public string Host { get => _Host; set { _Host = value; NotifyPropertyChanged(); } }
        private string _Host = "127.0.0.1";


        /// <summary>
        /// 端口地址
        /// </summary>
        [DisplayName("端口地址")]
        public int Port
        {
            get => _Port; set
            {
                _Port = value <= 0 ? 0 : value >= 65535 ? 65535 : value;
                 
                NotifyPropertyChanged();
            }
        }
        private int _Port = Common.Utilities.Tool.GetFreePort(9002);

        [Browsable(false)]
        public long Capacity
        {
            get => _Capacity;
            set
            {
                if (_Capacity != value)
                {
                    _Capacity = value;
                    NotifyPropertyChanged();
                    NotifyPropertyChanged(nameof(CapacityText));
                }
            }
        }
        private long _Capacity = 1073741824;

        [JsonIgnore]
        [DisplayName("缓存大小")]
        public string CapacityText
        {
            get => Common.Utilities.MemorySize.MemorySizeText(_Capacity);
            set
            {
                if (Common.Utilities.MemorySize.TryParseMemorySize(value, out long parsedValue))
                {
                    Capacity = parsedValue;
                }
            }
        }

    }
}
