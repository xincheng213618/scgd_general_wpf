namespace MQTTMessageLib.Camera;

public class MQTTCameraOpen : MQTTCVRequestTokenHeader
{
	public MQTTCameraOpen(string serviceName, string serialNumber)
		: base(serviceName, "Open", serialNumber)
	{
	}
}
