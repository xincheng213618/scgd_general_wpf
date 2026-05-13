namespace MQTTMessageLib.Algorithm.XR;

public class DistortionGetDataParam : DeviceAlgorithmParam
{
	public DeviceParamDistortion DeviceParam { get; set; }

	public DistortionGetDataParam()
	{
		DeviceParam = new DeviceParamDistortion();
	}
}
