namespace MQTTMessageLib.SMU;

public class MQTTSMUModelResponse : MQTTCVBaseResponse<MQTTSMUModelResultData>
{
	public MQTTSMUModelResponse(MQTTCVRequestHeader request, DeviceSMUModelMeasureResponse response)
		: base(request, new MQTTCVResponseStatus(response), new MQTTSMUModelResultData(response))
	{
	}
}
