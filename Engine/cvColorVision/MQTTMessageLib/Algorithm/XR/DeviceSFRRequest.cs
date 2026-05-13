namespace MQTTMessageLib.Algorithm.XR;

public class DeviceSFRRequest : DeviceAlgorithmBaseRequest<SFRGetDataParam>
{
	public DeviceSFRRequest(string deviceName, string serialNumber, int zIndex, SFRGetDataParam param)
		: base(deviceName, serialNumber, zIndex, AlgorithmRequestType.SFR_GetData, param)
	{
	}
}
