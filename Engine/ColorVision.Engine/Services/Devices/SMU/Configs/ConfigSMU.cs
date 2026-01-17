using ColorVision.Engine.Services.Devices.SMU.Dao;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.SMU.Configs
{


    public class ConfigSMU : DeviceServiceConfig
    {
        [DisplayName("Is4Wire")]
        public bool Is4Wire { get => _Is4Wire; set { _Is4Wire = value; OnPropertyChanged(); } }
        private bool _Is4Wire;

        [DisplayName("IsFront")]
        public bool IsFront { get => _IsFront; set { _IsFront = value; OnPropertyChanged(); } }
        private bool _IsFront;

        [DisplayName("IsSrcA")]
        public bool IsSrcA { get => _IsSrcA; set { _IsSrcA = value; OnPropertyChanged(); } }
        private bool _IsSrcA;

        public bool IsNet { get => _IsNet; set { _IsNet = value; OnPropertyChanged(); } }
        private bool _IsNet;

        public bool IsAutoStart { get => _IsAutoStart; set { _IsAutoStart = value; OnPropertyChanged(); } }
        private bool _IsAutoStart;

        public string DevName { get => Id; set { Id = value; OnPropertyChanged(); } }

        public string DevType { get => _DevType; set { _DevType = value; OnPropertyChanged(); } }
        private string _DevType;


    }
}
