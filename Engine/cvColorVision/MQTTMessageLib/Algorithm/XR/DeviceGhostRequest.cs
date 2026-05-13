namespace MQTTMessageLib.Algorithm.XR;

public class DeviceGhostRequest : DeviceAlgorithmBaseRequest<GhostGetDataParam>
{
	public DeviceGhostRequest(string deviceName, string serialNumber, int zIndex, GhostGetDataParam param)
		: base(deviceName, serialNumber, zIndex, AlgorithmRequestType.Ghost_GetData, param)
	{
	}
}
