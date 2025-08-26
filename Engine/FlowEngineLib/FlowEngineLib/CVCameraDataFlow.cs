using FlowEngineLib.Base;

namespace FlowEngineLib;

public class CVCameraDataFlow : CVMQTTRequest
{
	public CVCameraDataFlow(string serviceName, string deviceName, string eventName, string sn, object data, string token)
		: base(serviceName, deviceName, eventName, sn, data, token)
	{
	}
}
