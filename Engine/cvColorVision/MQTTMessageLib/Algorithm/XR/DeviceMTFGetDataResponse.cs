using System.Collections.Generic;
using CVCommCore;

namespace MQTTMessageLib.Algorithm.XR;

public class DeviceMTFGetDataResponse : DeviceAlgorithmGetDataResponse<List<MTFResult>>
{
	public DeviceMTFGetDataResponse(string imgFileName, string templateName, List<MTFResult> data, CVBaseDeviceResponse status, long totalTime)
		: base(AlgorithmResultType.MTF, imgFileName, templateName, data, status, totalTime)
	{
	}
}
