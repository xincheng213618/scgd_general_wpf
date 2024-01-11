using cvColorVision;
using ColorVision.MVVM;

namespace ColorVision.Services.Device.Camera.Configs
{
    public class CFWPORT : ViewModelBase
    {
        public ChannelCfg[] ChannelCfgs { get; set; } = new ChannelCfg[3]{
            new ChannelCfg() { Cfwport =0,Chtype =ImageChannelType.Gray_Y }, new ChannelCfg(){Cfwport =1,Chtype =ImageChannelType.Gray_X }, new ChannelCfg(){ Cfwport =2,Chtype =ImageChannelType.Gray_Z}
        };

        public bool IsCOM { get => _IsCOM; set { _IsCOM = value; NotifyPropertyChanged(); } }
        private bool _IsCOM;

        public string SzComName { get => _szComName; set { _szComName = value; NotifyPropertyChanged(); } }
        private string _szComName = "COM1";
        public int BaudRate { get => _BaudRate; set { _BaudRate = value; NotifyPropertyChanged(); } }
        private int _BaudRate = 115200;
    }
}