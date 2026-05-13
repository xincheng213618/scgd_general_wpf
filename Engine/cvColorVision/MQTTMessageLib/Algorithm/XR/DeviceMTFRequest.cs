namespace MQTTMessageLib.Algorithm.XR;

public class DeviceMTFRequest : DeviceAlgorithmBaseRequest<MTFGetDataParam>
{
	public DeviceMTFRequest(string deviceName, string serialNumber, int zIndex, MTFGetDataParam param)
		: base(deviceName, serialNumber, zIndex, AlgorithmRequestType.MTF_GetData, param)
	{
	}
}
