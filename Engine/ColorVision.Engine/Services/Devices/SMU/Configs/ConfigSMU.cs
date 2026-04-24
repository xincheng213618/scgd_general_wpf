using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using cvColorVision;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.SMU.Configs
{

    public class ConfigSMU : DeviceServiceConfig
    {
        public bool IsAutoStart { get => _IsAutoStart; set { _IsAutoStart = value; OnPropertyChanged(); } }
        private bool _IsAutoStart;

        public Pss_Type DevType { get => _DevType; set { _DevType = value; OnPropertyChanged(); OnPropertyChanged(nameof(DevType)); } }
        private Pss_Type _DevType;

        public bool IsNet { get => _IsNet; set { _IsNet = value; OnPropertyChanged(); } }
        private bool _IsNet;

        [PropertyEditorType(typeof(TextSerialPortPropertiesEditor))]
        public string DevName { get => Id; set { Id = value; OnPropertyChanged(); } }


        [DisplayName("Is4Wire")]
        public bool Is4Wire { get => _Is4Wire; set { _Is4Wire = value; OnPropertyChanged(); } }
        private bool _Is4Wire;

        [DisplayName("IsFront")]
        public bool IsFront { get => _IsFront; set { _IsFront = value; OnPropertyChanged(); } }
        private bool _IsFront;

        [DisplayName("IsSrcA")]
        public bool IsSrcA { get => _IsSrcA; set { _IsSrcA = value; OnPropertyChanged(); } }
        private bool _IsSrcA = true;

        [DisplayName("DelayTime")]
        public double DelayTime { get => _DelayTime; set { _DelayTime = value; OnPropertyChanged(); } }
        private double _DelayTime;
    }
}
