namespace MQTTMessageLib;

public class MQTTCVNullResponse : MQTTCVResponseHeader
{
	public MQTTCVNullResponse(MQTTCVRequestHeader request, MQTTCVResponseStatus status)
		: base(request, status.Code, status.Desc)
	{
	}

	public MQTTCVNullResponse(MQTTCVRequestHeader request, IDeviceResponse status)
		: this(request, new MQTTCVResponseStatus(status))
	{
	}

	public MQTTCVNullResponse()
		: this(new MQTTCVRequestHeader(), new MQTTCVResponseStatus())
	{
	}
}
