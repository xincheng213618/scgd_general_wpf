using Newtonsoft.Json;

namespace MQTTMessageLib.Camera;

public class GetAutoExpTimeParam
{
	public bool IsWithND { get; set; }

	public CVTemplateParam AutoExpTimeTemplate { get; set; }

	[JsonIgnore]
	public DeviceParamCameraAutoExpTime AutoExpTimeCfg { get; set; }
}
