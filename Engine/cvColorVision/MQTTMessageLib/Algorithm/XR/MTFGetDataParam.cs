namespace MQTTMessageLib.Algorithm.XR;

public class MTFGetDataParam : DeviceAlgorithmParam
{
	public DeviceParamMTF DeviceParam { get; set; }

	public MTFGetDataParam()
	{
		DeviceParam = new DeviceParamMTF();
	}
}
