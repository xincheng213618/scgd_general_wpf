using CVCommCore;

namespace MQTTMessageLib.Algorithm.XR;

public class DeviceGhostGetDataResponse : DeviceAlgorithmGetDataResponse<GhostResult>
{
	public DeviceGhostGetDataResponse(string imgFileName, string templateName, GhostResult data, CVBaseDeviceResponse status, long totalTime)
		: base(AlgorithmResultType.Ghost, imgFileName, templateName, data, status, totalTime)
	{
	}
}
