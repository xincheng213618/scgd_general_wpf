using ColorVision.Engine.Cache;
using System.ComponentModel;

namespace ColorVision.Engine.Services.Devices.Calibration
{
    public class ConfigCalibration: DeviceServiceConfig, IFileServerCfg
    {
        [Browsable(false)]
        public override string SN { get => base.SN; set => base.SN = value; }

        [PropertyEditorType(typeof(TextSNPropertiesEditor)),DisplayName("SN"), Category("Base")]
        public string? CameraCode { get => _CameraCode; set { _CameraCode = value; OnPropertyChanged(); } }
        private string? _CameraCode;

        [Browsable(false)]
        public string CameraID { get => _CameraID; set { _CameraID = value; OnPropertyChanged(); } }
        private string _CameraID;



        public FileServerCfg FileServerCfg { get; set; } = new FileServerCfg();
    }
}
