using System.Collections.Generic;
using CVCommCore;

namespace MQTTMessageLib.Algorithm.POI;

public class DevicePOIGetDataCIEYResponse : DevicePOIGetDataResponse<List<POIResultCIEYList>>
{
	public DevicePOIGetDataCIEYResponse(string _POIImgFileName, string _POITemplateName, CVBaseDeviceResponse status, bool isAdd, List<POIResultCIEYList> data, long totalTime)
		: base(AlgorithmResultType.POI_Y, _POIImgFileName, _POITemplateName, status, isAdd, data, totalTime)
	{
	}

	public static DevicePOIGetDataCIEYResponse Success(string _POIImgFileName, string _POITemplateName, bool isAdd, List<POIResultCIEYList> data, long totalTime)
	{
		return new DevicePOIGetDataCIEYResponse(_POIImgFileName, _POITemplateName, CVBaseDeviceResponse.Success(), isAdd, data, totalTime);
	}
}
