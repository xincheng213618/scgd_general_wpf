using CVCommCore;

namespace MQTTMessageLib.Algorithm.POI;

public class MQTTPOIGetDataResponse : MQTTCVBaseResponse<MQTTPOIGetDataResult>
{
	public MQTTPOIGetDataResponse(MQTTCVRequestHeader request, DevicePOIGetDataCIExyuvResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), new MQTTPOIGetDataResult(response.ImgFileName, response.TemplateName, AlgorithmResultType.POI_XYZ, response.Data[0].Data.Count, hasRecord: false, response.MasterId))
	{
	}

	public MQTTPOIGetDataResponse(MQTTCVRequestHeader request, DevicePOIGetDataCIEYResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), new MQTTPOIGetDataResult(response.ImgFileName, response.TemplateName, AlgorithmResultType.POI_Y, response.Data[0].Data.Count, hasRecord: false, response.MasterId))
	{
	}
}
