using System.Collections.Generic;
using CVCommCore;

namespace MQTTMessageLib.Algorithm.POI;

public class DevicePOIGetDataCIExyuvResponse : DevicePOIGetDataResponse<List<POIResultCIExyuvList>>
{
	public DevicePOIGetDataCIExyuvResponse(string _POIImgFileName, string _POITemplateName, CVBaseDeviceResponse status, bool isAdd, List<POIResultCIExyuvList> data, long totalTime)
		: base(AlgorithmResultType.POI_XYZ, _POIImgFileName, _POITemplateName, status, isAdd, data, totalTime)
	{
	}

	public static DevicePOIGetDataCIExyuvResponse Success(string _POIImgFileName, string _POITemplateName, bool isAdd, List<POIResultCIExyuvList> data, long totalTime)
	{
		return new DevicePOIGetDataCIExyuvResponse(_POIImgFileName, _POITemplateName, CVBaseDeviceResponse.Success(), isAdd, data, totalTime);
	}
}
