namespace MQTTMessageLib.SMU;

public class MQTTSMUResponse : MQTTCVBaseResponse<MQTTSMUResultData>
{
	public MQTTSMUResponse(MQTTCVRequestHeader request, DeviceSMUMeasureResponse response)
		: base(request, new MQTTCVResponseStatus(response), new MQTTSMUResultData(response))
	{
	}
}
