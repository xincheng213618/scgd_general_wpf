using MQTTMessageLib.Algorithm;
using Newtonsoft.Json;

namespace MQTTMessageLib.Camera;

public class DeviceCameraHDRParam : ParamDicBase
{
	public CameraHDRParam ToHDRCfg()
	{
		return JsonConvert.DeserializeObject<CameraHDRParam>(base.JsonConfig);
	}
}
