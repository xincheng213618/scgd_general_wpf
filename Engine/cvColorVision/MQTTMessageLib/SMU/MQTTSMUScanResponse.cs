namespace MQTTMessageLib.SMU;

public class MQTTSMUScanResponse : MQTTCVBaseResponse<MQTTSMUScanResultData>
{
	public MQTTSMUScanResponse(MQTTCVRequestHeader request, DeviceSMUScanResponse response)
		: base(request, new MQTTCVResponseStatus(response), new MQTTSMUScanResultData(response))
	{
	}
}
