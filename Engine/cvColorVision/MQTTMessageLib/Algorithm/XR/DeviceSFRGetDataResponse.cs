using System.Collections.Generic;
using CVCommCore;

namespace MQTTMessageLib.Algorithm.XR;

public class DeviceSFRGetDataResponse : DeviceAlgorithmGetDataResponse<List<SFRResult>>
{
	public DeviceSFRGetDataResponse(string imgFileName, string templateName, List<SFRResult> data, CVBaseDeviceResponse status, long totalTime)
		: base(AlgorithmResultType.SFR, imgFileName, templateName, data, status, totalTime)
	{
	}
}
