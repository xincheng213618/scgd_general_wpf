namespace MQTTMessageLib.Algorithm.LED;

public class MQTTLedCheckGetDataResponse : MQTTCVBaseResponse<MQTTLedCheckGetDataResult>
{
	public MQTTLedCheckGetDataResponse(MQTTCVRequestHeader request, DeviceLedCheckGetDataResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), new MQTTLedCheckGetDataResult(response.ImgFileName, response.TemplateName, response.Data, response.MasterId))
	{
	}
}
