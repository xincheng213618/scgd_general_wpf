namespace MQTTMessageLib.Camera;

public class MQTTCameraMotorAutoFocus : MQTTCVBaseRequest<AutoFocusRunParam>
{
	public MQTTCameraMotorAutoFocus(string serviceName, string serialNumber, AutoFocusRunParam data)
		: base(serviceName, "AutoFocus", serialNumber, data)
	{
	}
}
