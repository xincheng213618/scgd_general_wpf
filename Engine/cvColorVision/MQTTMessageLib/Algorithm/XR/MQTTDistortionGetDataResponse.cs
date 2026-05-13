namespace MQTTMessageLib.Algorithm.XR;

public class MQTTDistortionGetDataResponse : MQTTCVBaseResponse<MQTTDistortionGetDataResult>
{
	public MQTTDistortionGetDataResponse(MQTTCVRequestHeader request, DeviceDistortionGetDataResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), new MQTTDistortionGetDataResult(response.ImgFileName, response.TemplateName, response.Data, response.MasterId))
	{
	}
}
