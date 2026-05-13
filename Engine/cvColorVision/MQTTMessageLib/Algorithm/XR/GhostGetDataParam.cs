using CVCommCore.CVImage;

namespace MQTTMessageLib.Algorithm.XR;

public class GhostGetDataParam : DeviceAlgorithmParam
{
	public CVOLED_COLOR Color { get; set; }

	public DeviceParamGhost DeviceParam { get; set; }

	public GhostGetDataParam()
	{
		DeviceParam = new DeviceParamGhost();
	}
}
