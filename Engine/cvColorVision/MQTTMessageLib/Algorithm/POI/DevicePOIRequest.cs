namespace MQTTMessageLib.Algorithm.POI;

public class DevicePOIRequest : DeviceAlgorithmBaseRequest<POIGetDataParam>
{
	public DevicePOIRequest(string deviceName, string serialNumber, int zIndex, POIGetDataParam param, bool isImageFilePreRequest = true, bool isPersistence = true)
		: base(deviceName, serialNumber, zIndex, AlgorithmRequestType.POI_GetData, param)
	{
		base.IsImageFilePreRequest = isImageFilePreRequest;
		base.IsPersistence = isPersistence;
	}
}
