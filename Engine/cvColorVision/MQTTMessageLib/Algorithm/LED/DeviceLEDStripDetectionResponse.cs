using System.Collections.Generic;
using CVCommCore;

namespace MQTTMessageLib.Algorithm.LED;

public class DeviceLEDStripDetectionResponse : DeviceAlgorithmGetDataResponse<List<LEDStripDetectionResult>>
{
	public DeviceLEDStripDetectionResponse(string imgFileName, string templateName, List<LEDStripDetectionResult> data, CVBaseDeviceResponse status, long totalTime)
		: base(AlgorithmResultType.LEDStripDetection, imgFileName, templateName, data, status, totalTime)
	{
	}

	public static DeviceLEDStripDetectionResponse Success(string imgFileName, string templateName, List<LEDStripDetectionResult> data, long totalTime)
	{
		return new DeviceLEDStripDetectionResponse(imgFileName, templateName, data, CVBaseDeviceResponse.Success(), totalTime);
	}
}
