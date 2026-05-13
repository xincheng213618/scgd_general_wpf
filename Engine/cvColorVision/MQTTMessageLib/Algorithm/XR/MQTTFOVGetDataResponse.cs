namespace MQTTMessageLib.Algorithm.XR;

public class MQTTFOVGetDataResponse : MQTTCVBaseResponse<MQTTFOVGetDataResult>
{
	public MQTTFOVGetDataResponse(MQTTCVRequestHeader request, DeviceFOVGetDataResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), new MQTTFOVGetDataResult(response.ImgFileName, response.TemplateName, response.Data, response.MasterId))
	{
	}
}
