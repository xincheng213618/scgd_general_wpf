using ColorVision.Common.MVVM;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Services.Devices.CfwPort;
using cvColorVision;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace ColorVision.Engine.Services.PhyCameras.Configs
{

    public class CFWPORT : ViewModelBase
    {
        public CFWPORT()
        {
            _ChannelCfgs = new List<ChannelCfg> { };
        }

        public CFWPORT CloneForEdit()
        {
            var copy = this.Clone();
            copy.ChannelCfgs = ChannelCfgs?
                .Select(item => new ChannelCfg { Cfwport = item.Cfwport, Chtype = item.Chtype })
                .ToList() ?? new List<ChannelCfg>();
            if (copy.IsCOM && copy.IsBingNDDevice)
            {
                copy.IsBingNDDevice = false;
            }

            return copy;
        }

        public void EnsureChannelCfgsForEdit()
        {
            ChannelCfgs ??= new List<ChannelCfg>();

            if (ChannelCfgs.Count == 0)
            {
                AddDefaultChannelCfgs();
            }

            while (ChannelCfgs.Count < 9)
            {
                ChannelCfgs.Add(new ChannelCfg());
            }

            OnPropertyChanged(nameof(ChannelCfgs));
        }

        public void NormalizeChannelCfgsForSave()
        {
            EnsureChannelCfgsForEdit();

            if (CFWNum > 1)
            {
                ChannelCfgs[3].Chtype = ChannelCfgs[0].Chtype;
                ChannelCfgs[4].Chtype = ChannelCfgs[1].Chtype;
                ChannelCfgs[5].Chtype = ChannelCfgs[2].Chtype;
            }

            if (CFWNum > 2)
            {
                ChannelCfgs[6].Chtype = ChannelCfgs[0].Chtype;
                ChannelCfgs[7].Chtype = ChannelCfgs[1].Chtype;
                ChannelCfgs[8].Chtype = ChannelCfgs[2].Chtype;
            }

            int targetCount = CFWNum switch
            {
                1 => 3,
                2 => 6,
                _ => 9,
            };

            if (ChannelCfgs.Count > targetCount)
            {
                ChannelCfgs = ChannelCfgs.GetRange(0, targetCount);
            }
        }

        private void AddDefaultChannelCfgs()
        {
            ChannelCfgs.Add(new ChannelCfg { Cfwport = 0, Chtype = ImageChannelType.Gray_Y });
            ChannelCfgs.Add(new ChannelCfg { Cfwport = 1, Chtype = ImageChannelType.Gray_X });
            ChannelCfgs.Add(new ChannelCfg { Cfwport = 2, Chtype = ImageChannelType.Gray_Z });
        }

        public bool IsUseCFW { get => _IsUseCFW; set { _IsUseCFW = value; OnPropertyChanged(); } }
        private bool _IsUseCFW;

        public bool IsBingNDDevice
        {
            get => _IsBingNDDevice;
            set
            {
                if (_IsBingNDDevice == value)
                {
                    return;
                }

                _IsBingNDDevice = value;
                if (value && _IsCOM)
                {
                    _IsCOM = false;
                    OnPropertyChanged(nameof(IsCOM));
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSerialPortVisible));
            }
        }
        private bool _IsBingNDDevice = true;

        [PropertyEditorType(typeof(DeviceNameEditor)), DeviceSourceType(typeof(DeviceCfwPort)), PropertyVisibility(nameof(IsBingNDDevice))]
        public string NDBindDeviceCode { get => _NDBindDeviceCode; set { _NDBindDeviceCode = value; OnPropertyChanged(); } }
        private string _NDBindDeviceCode = "";


        public bool IsCOM
        {
            get => _IsCOM;
            set
            {
                if (_IsCOM == value)
                {
                    return;
                }

                _IsCOM = value;
                if (value && _IsBingNDDevice)
                {
                    _IsBingNDDevice = false;
                    OnPropertyChanged(nameof(IsBingNDDevice));
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSerialPortVisible));
            }
        }
        private bool _IsCOM;

        public bool IsSerialPortVisible => IsCOM && !IsBingNDDevice;

        [PropertyEditorType(typeof(TextSerialPortPropertiesEditor)), PropertyVisibility(nameof(IsSerialPortVisible))]
        public string SzComName { get => _szComName; set { _szComName = value; OnPropertyChanged(); } }
        private string _szComName = "COM1";

        [PropertyEditorType(typeof(TextBaudRatePropertiesEditor)), PropertyVisibility(nameof(IsSerialPortVisible))]
        public int BaudRate { get => _BaudRate; set { _BaudRate = value; OnPropertyChanged(); } }
        private int _BaudRate = 9600;

        public int CFWNum
        {
            get => _CFWNum; set
            {
                if (_CFWNum == value) return;
                if (value > 3)
                {
                    _CFWNum = 3;
                    OnPropertyChanged();
                    return;
                }

                if (value < 1)
                {
                    _CFWNum = 1;
                    OnPropertyChanged();
                    return;
                }
                _CFWNum = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCFWNum1));
                OnPropertyChanged(nameof(IsCFWNum2));
                OnPropertyChanged(nameof(IsCFWNum3));
            }
        }
        private int _CFWNum = 1;

        public bool IsCFWNum1 => CFWNum == 1;
        public bool IsCFWNum2 => CFWNum >= 2;
        public bool IsCFWNum3 => CFWNum >= 3;

        public List<ChannelCfg> ChannelCfgs { get => _ChannelCfgs; set { _ChannelCfgs = value; OnPropertyChanged(); } }

        private List<ChannelCfg> _ChannelCfgs;

        public bool EnableResetND { get => _EnableResetND; set { _EnableResetND = value; OnPropertyChanged(); } }
        private bool _EnableResetND;

        public bool IsNDPort { get => _IsNDPort; set { _IsNDPort = value; OnPropertyChanged(); } }
        private bool _IsNDPort;

        public double NDMaxExpTime { get => _NDMaxExpTime; set { _NDMaxExpTime = value; OnPropertyChanged(); } }
        private double _NDMaxExpTime;
        public double NDMinExpTime { get => _NDMinExpTime; set { _NDMinExpTime = value; OnPropertyChanged(); } }
        private double _NDMinExpTime;

        public List<int> NDRate { get; set; } = new List<int>();

        [CollectionEditorType(typeof(TextSelectFilePropertiesEditor))]
        public List<string> NDCaliNameGroups { get; set; } = new List<string>();

    }
}
