using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using ColorVision.Engine.Services.PhyCameras.Configs;
using cvColorVision;
using Newtonsoft.Json;

namespace ColorVision.Engine.Services.Devices.Motor
{
    public class ConfigMotor: DeviceServiceConfig
    {
        public MotorConfig MotorConfig { get; set; } = new MotorConfig();
        [JsonIgnore]
        public int Position { get => _Position;set { _Position = value; NotifyPropertyChanged(); } }
        private int _Position;


    }
}
