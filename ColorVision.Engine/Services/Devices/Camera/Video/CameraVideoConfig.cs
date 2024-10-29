using ColorVision.Common.MVVM;
using Newtonsoft.Json;

namespace ColorVision.Engine.Services.Devices.Camera.Video
{
    public class CameraVideoConfig : ViewModelBase
    {
        /// <summary>
        /// IP地址
        /// </summary>
        public string Host { get => _Host; set { _Host = value; NotifyPropertyChanged(); } }
        private string _Host = "127.0.0.1";

        public bool IsEnableResize { get => _IsEnableResize; set { _IsEnableResize = value; NotifyPropertyChanged(); } }
        private bool _IsEnableResize;

        public float ResizeRatio { get => _ResizeRatio; set { _ResizeRatio = value; NotifyPropertyChanged(); } }
        private float _ResizeRatio;

        /// <summary>
        /// 端口地址
        /// </summary>
        public int Port
        {
            get => _Port; set
            {
                _Port = value <= 0 ? 0 : value >= 65535 ? 65535 : value;
                NotifyPropertyChanged();
            }
        }
        private int _Port = 9002;

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
                    NotifyPropertyChanged(nameof(CapacityInput));
                }
            }
        }
        private long _Capacity = 1073741824;

        [JsonIgnore]
        public string CapacityText => Common.Utilities.MemorySize.MemorySizeText(_Capacity);

        [JsonIgnore]
        public string CapacityInput
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
