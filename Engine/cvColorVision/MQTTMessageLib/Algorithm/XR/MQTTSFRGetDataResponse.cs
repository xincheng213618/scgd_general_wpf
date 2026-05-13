namespace MQTTMessageLib.Algorithm.XR;

public class MQTTSFRGetDataResponse : MQTTCVBaseResponse<MQTTSFRGetDataResult>
{
	public MQTTSFRGetDataResponse(MQTTCVRequestHeader request, DeviceSFRGetDataResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), new MQTTSFRGetDataResult(response.ImgFileName, response.TemplateName, response.Data, response.MasterId))
	{
	}
}
