using CVCommCore;

namespace MQTTMessageLib.Algorithm;

public class DeviceAlgorithmGetDataResponse<T> : DeviceAlgorithmResponse
{
	public T Data { get; set; }

	public DeviceAlgorithmGetDataResponse(AlgorithmResultType resultType, string imgFileName, string templateName, T data, CVBaseDeviceResponse status, long totalTime)
		: this(resultType, imgFileName, templateName, data, (string)null, status, totalTime)
	{
	}

	public DeviceAlgorithmGetDataResponse(AlgorithmResultType resultType, string imgFileName, string templateName, T data, string outImgFile, CVBaseDeviceResponse status, long totalTime)
		: base(resultType, imgFileName, templateName, outImgFile, status, totalTime)
	{
		Data = data;
	}
}
