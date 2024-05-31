using cvColorVision;
using ColorVision.Common.MVVM;

namespace ColorVision.Engine.Services.PhyCameras.Configs
{
    public class CFWPORT : ViewModelBase
    {
        public ChannelCfg[] ChannelCfgs { get; set; } = new ChannelCfg[3]{
            new() { Cfwport =0,Chtype =ImageChannelType.Gray_Y }, new(){Cfwport =1,Chtype =ImageChannelType.Gray_X }, new(){ Cfwport =2,Chtype =ImageChannelType.Gray_Z}
        };
        public bool IsCOM { get => _IsCOM; set { _IsCOM = value; NotifyPropertyChanged(); } }
        private bool _IsCOM;

        public string SzComName { get => _szComName; set { _szComName = value; NotifyPropertyChanged(); } }
        private string _szComName = "COM1";

        public int BaudRate { get => _BaudRate; set { _BaudRate = value; NotifyPropertyChanged(); } }
        private int _BaudRate = 115200;
    }
}