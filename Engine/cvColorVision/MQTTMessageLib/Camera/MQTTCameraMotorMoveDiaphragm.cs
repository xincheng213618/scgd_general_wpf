namespace MQTTMessageLib.Camera;

public class MQTTCameraMotorMoveDiaphragm : MQTTCVBaseRequest<MotorMoveDiaphragmParam>
{
	public MQTTCameraMotorMoveDiaphragm(string serviceName, string serialNumber, MotorMoveDiaphragmParam data)
		: base(serviceName, "MoveDiaphragm", serialNumber, data)
	{
	}
}
