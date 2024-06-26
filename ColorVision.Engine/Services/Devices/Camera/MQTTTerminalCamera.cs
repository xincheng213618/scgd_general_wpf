using ColorVision.Engine.Services.Terminal;

namespace ColorVision.Engine.Services.Devices.Camera
{
    public class MQTTTerminalCamera : MQTTServiceTerminalBase<TerminalServiceConfig>
    {

        public MQTTTerminalCamera(TerminalServiceConfig Config) :base(Config)
        {
        }
    }
}
