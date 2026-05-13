using System.Collections.Generic;
using CVCommCore;

namespace MQTTMessageLib.Algorithm.XR;

public class DeviceFOVGetDataResponse : DeviceAlgorithmGetDataResponse<List<FOVResult>>
{
	public DeviceFOVGetDataResponse(string imgFileName, string templateName, List<FOVResult> data, CVBaseDeviceResponse status, long totalTime)
		: base(AlgorithmResultType.FOV, imgFileName, templateName, data, status, totalTime)
	{
	}
}
