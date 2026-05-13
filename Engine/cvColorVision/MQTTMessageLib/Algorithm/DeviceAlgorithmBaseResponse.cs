using CVCommCore;

namespace MQTTMessageLib.Algorithm;

public class DeviceAlgorithmBaseResponse : CVBaseDeviceResponseWithResult, IDevAlgorithmResponse, IDeviceResponseWithResult, IDeviceResponse
{
	public AlgorithmResultType ResultType { get; set; }

	public string OutImgFileName { get; set; }

	public IDevAlgorithmRequest NextRequest { get; set; }

	public DeviceAlgorithmBaseResponse(AlgorithmResultType resultType, CVBaseDeviceResponse status, long totalTime)
		: this(resultType, string.Empty, status, totalTime)
	{
	}

	public DeviceAlgorithmBaseResponse(AlgorithmResultType resultType, string outFileName, CVBaseDeviceResponse status, long totalTime)
		: base((int)resultType, status, totalTime)
	{
		ResultType = resultType;
		OutImgFileName = outFileName;
	}

	public DeviceAlgorithmBaseResponse(AlgorithmResultType resultType, int code, string desc, long totalTime)
		: this(resultType, string.Empty, code, desc, totalTime)
	{
	}

	public DeviceAlgorithmBaseResponse(AlgorithmResultType resultType, int code, string resultCode, string desc, long totalTime)
		: this(resultType, code, desc, totalTime)
	{
		base.DeviceResultCode = resultCode;
	}

	public DeviceAlgorithmBaseResponse(AlgorithmResultType resultType, string outFileName, int code, string desc, long totalTime)
		: base((int)resultType, code, desc, totalTime)
	{
		ResultType = resultType;
		OutImgFileName = outFileName;
	}
}
