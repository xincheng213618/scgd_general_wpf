namespace MQTTMessageLib.Algorithm.XR;

public class DeviceFOVRequest : DeviceAlgorithmBaseRequest<FOVGetDataParam>
{
	public DeviceFOVRequest(string deviceName, string serialNumber, int zIndex, FOVGetDataParam param)
		: base(deviceName, serialNumber, zIndex, AlgorithmRequestType.FOV_GetData, param)
	{
	}
}
