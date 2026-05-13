namespace MQTTMessageLib.Algorithm.POI;

public class MQTTBuildPOIResponse : MQTTCVBaseResponse<MQTTBuildPOIResult>
{
	public MQTTBuildPOIResponse(MQTTCVRequestHeader request, DeviceBuildPOIResponse response)
		: base(request, new MQTTCVResponseStatus(response.Code, response.Desc), new MQTTBuildPOIResult(response.ImgFileName, response.TemplateName, response.Data, response.MasterId))
	{
	}
}
