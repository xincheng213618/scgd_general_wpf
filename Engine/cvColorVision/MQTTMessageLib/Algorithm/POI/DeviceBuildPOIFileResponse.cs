using System.Collections.Generic;
using CVCommCore;
using CVCommCore.CVAlgorithm;

namespace MQTTMessageLib.Algorithm.POI;

public class DeviceBuildPOIFileResponse : DeviceAlgorithmGetDataResponse<List<POIPointOnly>>
{
	public DeviceBuildPOIFileResponse(string imgFileName, string templateName, List<POIPointOnly> data, CVBaseDeviceResponse status, long totalTime)
		: base(AlgorithmResultType.BuildPOI_File, imgFileName, templateName, data, status, totalTime)
	{
	}

	public static DeviceBuildPOIFileResponse Success(string imgFileName, string templateName, List<POIPointOnly> data, long totalTime)
	{
		return new DeviceBuildPOIFileResponse(imgFileName, templateName, data, CVBaseDeviceResponse.Success(), totalTime);
	}
}
