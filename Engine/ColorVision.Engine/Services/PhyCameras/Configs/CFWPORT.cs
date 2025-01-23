using ColorVision.Common.MVVM;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.PhyCameras.Configs
{
    public class CFWPORT : ViewModelBase
    {
        public CFWPORT()
        {
            _ChannelCfgs = new List<ChannelCfg> { };
        }

        public bool IsUseCFW { get => _IsUseCFW; set { _IsUseCFW = value; NotifyPropertyChanged(); } }
        private bool _IsUseCFW;

        public bool IsCOM { get => _IsCOM; set { _IsCOM = value; NotifyPropertyChanged(); } }
        private bool _IsCOM;

        public int CFWNum { get => _CFWNum; set {
                if (_CFWNum == value) return;
                if (value > 3)
                {
                    _CFWNum = 3;
                    NotifyPropertyChanged();
                    return;
                }

                if (value < 1)
                {
                    _CFWNum = 1;
                    NotifyPropertyChanged();
                    return;
                }
                _CFWNum = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(IsCFWNum1));
                NotifyPropertyChanged(nameof(IsCFWNum2));
                NotifyPropertyChanged(nameof(IsCFWNum3));
            } }
        private int _CFWNum = 1;

        public bool IsCFWNum1 => CFWNum == 1;
        public bool IsCFWNum2 => CFWNum >= 2;
        public bool IsCFWNum3 => CFWNum >= 3;


        public string SzComName { get => _szComName; set { _szComName = value; NotifyPropertyChanged(); } }
        private string _szComName = "COM1";

        public int BaudRate { get => _BaudRate; set { _BaudRate = value; NotifyPropertyChanged(); } }
        private int _BaudRate = 115200;

        public List<ChannelCfg> ChannelCfgs { get => _ChannelCfgs; set { _ChannelCfgs = value; NotifyPropertyChanged(); } }

        private List<ChannelCfg> _ChannelCfgs;
    }
}