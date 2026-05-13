namespace MQTTMessageLib.Algorithm.POI;

public class DeviceRealPOIRequest : DeviceAlgorithmBaseRequest<RealPOIGetDataParam>
{
	public DeviceRealPOIRequest(string deviceCode, string serialNumber, int zIndex, RealPOIGetDataParam param)
		: base(deviceCode, serialNumber, zIndex, AlgorithmRequestType.RealPOI_GetData, param)
	{
	}
}
