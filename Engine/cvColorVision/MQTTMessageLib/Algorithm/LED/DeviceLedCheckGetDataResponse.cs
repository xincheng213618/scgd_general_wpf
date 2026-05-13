using System.Collections.Generic;
using CVCommCore;

namespace MQTTMessageLib.Algorithm.LED;

public class DeviceLedCheckGetDataResponse : DeviceAlgorithmGetDataResponse<List<LedCheckResult>>
{
	public DeviceLedCheckGetDataResponse(string imgFileName, string templateName, List<LedCheckResult> data, CVBaseDeviceResponse status, long totalTime)
		: base(AlgorithmResultType.LedCheck, imgFileName, templateName, data, status, totalTime)
	{
	}
}
