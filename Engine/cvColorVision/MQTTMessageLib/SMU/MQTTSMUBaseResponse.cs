namespace MQTTMessageLib.SMU;

public class MQTTSMUBaseResponse : MQTTCVResponseHeader
{
	public MQTTSMUBaseResponse(MQTTCVRequestHeader request, IDeviceResponse respone)
		: base(request, respone.Code, respone.Desc)
	{
	}
}
