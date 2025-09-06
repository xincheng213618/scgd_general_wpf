using FlowEngineLib.Base;

namespace FlowEngineLib;

public class CVCameraDataFlow : CVMQTTRequest
{
	public CVCameraDataFlow(string serviceName, string deviceName, string eventName, string sn, object data, string token, int zIdx)
		: base(serviceName, deviceName, eventName, sn, data, token, zIdx)
	{
	}
}
