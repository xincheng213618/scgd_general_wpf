namespace MQTTMessageLib.Camera;

public class MQTTCameraGetData : MQTTCVBaseRequest<CameraGetDataParam>
{
	public MQTTCameraGetData(string serviceName, string serialNumber, CameraGetDataParam data)
		: base(serviceName, "GetData", serialNumber, data)
	{
	}
}
