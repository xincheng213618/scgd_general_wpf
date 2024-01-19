using Newtonsoft.Json;
using cvColorVision;
using ColorVision.MVVM;

namespace ColorVision.Services.Devices.Camera.Configs
{
    public class ChannelCfg : ViewModelBase
    {
        [JsonProperty("cfwport")]
        public int Cfwport { get => _Cfwport; set { _Cfwport = value; NotifyPropertyChanged(); } }
        private int _Cfwport;

        [JsonProperty("chtype")]
        public ImageChannelType Chtype { get => _Chtype; set { if (_Chtype == value) return; _Chtype = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(ChannelTypeString)); } }
        private ImageChannelType _Chtype;

        [JsonProperty("title")]
        public string ChannelTypeString
        {
            get
            {
                return Chtype switch
                {
                    ImageChannelType.Gray_X => "Channel_R",
                    ImageChannelType.Gray_Y => "Channel_G",
                    ImageChannelType.Gray_Z => "Channel_B",
                    _ => Chtype.ToString(),
                };
            }
            set { }
        }
    }
}