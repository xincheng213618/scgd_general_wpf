namespace MQTTMessageLib.Camera;

public class MQTTCameraOpenLive : MQTTCVBaseRequest<OpenLiveParam>
{
	public MQTTCameraOpenLive(string serviceName, string serialNumber, OpenLiveParam data)
		: base(serviceName, "OpenLive", serialNumber, data)
	{
	}
}
