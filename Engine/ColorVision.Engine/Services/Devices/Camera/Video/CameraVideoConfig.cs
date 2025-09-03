using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.Camera.Video
{
    public class CameraVideoConfig : ViewModelBase
    {
        public CameraVideoConfig()
        {
            DeafutPort++;
            _Port = Common.Utilities.Tool.GetFreePort(DeafutPort);
        }
        [DisplayName("主机")]
        public string Host { get => _Host; set { _Host = value; OnPropertyChanged(); } }
        private string _Host = "127.0.0.1";

        private static int DeafutPort { get; set; } = 9002;


        /// <summary>
        /// 端口地址
        /// </summary>
        [DisplayName("端口地址")]
        public int Port
        {
            get => _Port; set
            {
                _Port = value <= 0 ? 0 : value >= 65535 ? 65535 : value;
                 
                OnPropertyChanged();
            }
        }
        private int _Port;

        [Browsable(false)]
        public long Capacity
        {
            get => _Capacity;
            set
            {
                if (_Capacity != value)
                {
                    _Capacity = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CapacityText));
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
