using System.Collections.Generic;
using CVCommCore;

namespace MQTTMessageLib.Algorithm.XR;

public class DeviceDistortionGetDataResponse : DeviceAlgorithmGetDataResponse<List<DistortionResult>>
{
	public DeviceDistortionGetDataResponse(string imgFileName, string templateName, List<DistortionResult> data, long totalTime)
		: this(imgFileName, templateName, data, CVBaseDeviceResponse.Success(), totalTime)
	{
	}

	public DeviceDistortionGetDataResponse(string imgFileName, string templateName, List<DistortionResult> data, CVBaseDeviceResponse status, long totalTime)
		: base(AlgorithmResultType.Distortion, imgFileName, templateName, data, status, totalTime)
	{
	}
}
