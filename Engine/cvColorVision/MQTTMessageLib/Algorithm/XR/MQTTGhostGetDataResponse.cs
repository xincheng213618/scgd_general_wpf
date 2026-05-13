namespace MQTTMessageLib.Algorithm.XR;

public class MQTTGhostGetDataResponse : MQTTCVBaseResponse<MQTTGhostGetDataResult>
{
	public MQTTGhostGetDataResponse(MQTTCVRequestHeader request, DeviceGhostGetDataResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), new MQTTGhostGetDataResult(response.ImgFileName, response.TemplateName, response.Data, response.MasterId))
	{
	}
}
