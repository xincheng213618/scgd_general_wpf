using CVCommCore;

namespace MQTTMessageLib.Algorithm.POI;

public class DevicePOIGetDataResponse<T> : DeviceAlgorithmGetDataResponse<T>
{
	public bool IsAdd { get; set; }

	public DevicePOIGetDataResponse(AlgorithmResultType resultType, string _POIImgFileName, string _POITemplateName, CVBaseDeviceResponse status, bool isAdd, T data, long totalTime)
		: base(resultType, _POIImgFileName, _POITemplateName, data, status, totalTime)
	{
		IsAdd = isAdd;
		base.Data = data;
	}

	public static DevicePOIGetDataResponse<T> Success(AlgorithmResultType resultType, string _POIImgFileName, string _POITemplateName, bool isAdd, T Data, long totalTime)
	{
		return new DevicePOIGetDataResponse<T>(resultType, _POIImgFileName, _POITemplateName, CVBaseDeviceResponse.Success(), isAdd, Data, totalTime);
	}
}
