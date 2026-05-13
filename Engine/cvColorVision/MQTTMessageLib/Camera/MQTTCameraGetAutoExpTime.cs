namespace MQTTMessageLib.Camera;

public class MQTTCameraGetAutoExpTime : MQTTCVBaseRequest<GetAutoExpTimeParam>
{
	public MQTTCameraGetAutoExpTime(string serviceName, string serialNumber, GetAutoExpTimeParam data)
		: base(serviceName, "GetAutoExpTime", serialNumber, data)
	{
	}
}
