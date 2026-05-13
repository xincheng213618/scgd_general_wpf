namespace MQTTMessageLib.Algorithm.XR;

public class MQTTMTFGetDataResponse : MQTTCVBaseResponse<MQTTMTFGetDataResult>
{
	public MQTTMTFGetDataResponse(MQTTCVRequestHeader request, DeviceMTFGetDataResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), new MQTTMTFGetDataResult(response.ImgFileName, response.TemplateName, response.Data, response.MasterId))
	{
	}
}
