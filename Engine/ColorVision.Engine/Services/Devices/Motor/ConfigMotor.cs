using ColorVision.Engine.Services.PhyCameras.Configs;
using Newtonsoft.Json;

namespace ColorVision.Engine.Services.Devices.Motor
{
    public class ConfigMotor: DeviceServiceConfig
    {
        public MotorConfigBase MotorConfig { get; set; } = new MotorConfigBase();
        [JsonIgnore]
        public int Position { get => _Position;set { _Position = value; OnPropertyChanged(); } }
        private int _Position;


    }
}
