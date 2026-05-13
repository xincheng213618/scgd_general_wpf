using System.Collections.Generic;
using CVCommCore;
using CVCommCore.CVAlgorithm;

namespace MQTTMessageLib.Algorithm.POI;

public class DeviceBuildPOIResponse : DeviceAlgorithmGetDataResponse<List<POIPointOnly>>
{
	public DeviceBuildPOIResponse(string imgFileName, string templateName, List<POIPointOnly> data, CVBaseDeviceResponse status, long totalTime)
		: base(AlgorithmResultType.BuildPOI, imgFileName, templateName, data, status, totalTime)
	{
	}

	public static DeviceBuildPOIResponse Success(string imgFileName, string templateName, List<POIPointOnly> data, long totalTime)
	{
		return new DeviceBuildPOIResponse(imgFileName, templateName, data, CVBaseDeviceResponse.Success(), totalTime);
	}
}
