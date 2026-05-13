using CVCommCore.CVAlgorithm;

namespace MQTTMessageLib.Algorithm.XR;

public class FOVGetDataParam : DeviceAlgorithmParam
{
	public PointFloat PointCenter { get; set; }

	public PointFloat PointLeaning { get; set; }

	public DeviceParamFOV DeviceParam { get; set; }

	public FOVGetDataParam()
	{
		DeviceParam = new DeviceParamFOV();
	}
}
