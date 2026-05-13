namespace MQTTMessageLib.Camera;

public class MQTTCameraMotorMove : MQTTCVBaseRequest<MotorMoveParam>
{
	public MQTTCameraMotorMove(string serviceName, string serialNumber, MotorMoveParam data)
		: base(serviceName, "Move", serialNumber, data)
	{
	}
}
