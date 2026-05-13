namespace MQTTMessageLib.PG;

public class MQTTPGResponse : MQTTCVResponseHeader
{
	public MQTTPGResponse(MQTTCVRequestHeader request, IDeviceResponse respone)
		: base(request, respone.Code, respone.Desc)
	{
	}
}
