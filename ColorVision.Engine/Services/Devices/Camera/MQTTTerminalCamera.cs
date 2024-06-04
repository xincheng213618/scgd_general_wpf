using ColorVision.Services.Terminal;

namespace ColorVision.Services.Devices.Camera
{
    public class MQTTTerminalCamera : MQTTServiceTerminalBase<TerminalServiceConfig>
    {

        public MQTTTerminalCamera(TerminalServiceConfig Config) :base(Config)
        {
        }
    }
}
