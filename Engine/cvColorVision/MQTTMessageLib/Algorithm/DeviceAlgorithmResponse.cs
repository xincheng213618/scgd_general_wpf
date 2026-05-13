using CVCommCore;

namespace MQTTMessageLib.Algorithm;

public class DeviceAlgorithmResponse : DeviceAlgorithmBaseResponse
{
	public string ImgFileName { get; set; }

	public string TemplateName { get; set; }

	public DeviceAlgorithmResponse(AlgorithmResultType resultType, string imgFileName, string templateName, int code, string desc, long totalTime)
		: base(resultType, code, desc, totalTime)
	{
		ImgFileName = imgFileName;
		TemplateName = templateName;
	}

	public DeviceAlgorithmResponse(AlgorithmResultType resultType, string imgFileName, string templateName, CVBaseDeviceResponse status, long totalTime)
		: base(resultType, status, totalTime)
	{
		ImgFileName = imgFileName;
		TemplateName = templateName;
	}

	public DeviceAlgorithmResponse(AlgorithmResultType resultType, string imgFileName, string templateName, string outFileName, CVBaseDeviceResponse status, long totalTime)
		: base(resultType, outFileName, status, totalTime)
	{
		ImgFileName = imgFileName;
		TemplateName = templateName;
	}

	public static DeviceAlgorithmBaseResponse Failed(AlgorithmResultType resultType, string desc)
	{
		return Failed(resultType, desc, 0L);
	}

	public static DeviceAlgorithmBaseResponse Failed(AlgorithmResultType resultType, string desc, long totalTime)
	{
		return new DeviceAlgorithmBaseResponse(resultType, -1, desc, totalTime);
	}

	public static DeviceAlgorithmBaseResponse Failed(AlgorithmResultType resultType, string errCode, string desc, long totalTime)
	{
		return new DeviceAlgorithmBaseResponse(resultType, -1, errCode, desc, totalTime);
	}

	public static DeviceAlgorithmBaseResponse Success(AlgorithmResultType resultType, long totalTime)
	{
		return new DeviceAlgorithmBaseResponse(resultType, 0, "ok", totalTime);
	}
}
