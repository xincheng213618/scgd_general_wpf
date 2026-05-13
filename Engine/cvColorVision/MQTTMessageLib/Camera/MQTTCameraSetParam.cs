namespace MQTTMessageLib.Camera;

public class MQTTCameraSetParam : MQTTCVBaseRequest<SetParamParam>
{
	public MQTTCameraSetParam(string serviceName, string serialNumber, SetParamParam data)
		: base(serviceName, "SetParam", serialNumber, data)
	{
	}
}
