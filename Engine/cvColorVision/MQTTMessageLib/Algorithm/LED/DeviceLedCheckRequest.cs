namespace MQTTMessageLib.Algorithm.LED;

public class DeviceLedCheckRequest : DeviceAlgorithmBaseRequest<LedCheckParam>
{
	public DeviceLedCheckRequest(string deviceName, string serialNumber, int zIndex, LedCheckParam param)
		: base(deviceName, serialNumber, zIndex, AlgorithmRequestType.LedCheck_GetData, param)
	{
	}
}
