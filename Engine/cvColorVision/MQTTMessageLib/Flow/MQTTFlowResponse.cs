namespace MQTTMessageLib.Flow;

public class MQTTFlowResponse : MQTTCVBaseResponse<MQTTFlowRespResult>
{
	public MQTTFlowResponse(MQTTCVRequestHeader request, IDevFlowResponse response)
		: this(request, new MQTTCVResponseStatus(response), new MQTTFlowRespResult(response))
	{
	}

	public MQTTFlowResponse(MQTTCVRequestHeader request, MQTTCVResponseStatus status, MQTTFlowRespResult data)
		: base(request, status, data)
	{
	}

	public MQTTFlowResponse(string serviceName, string deviceName, string eventName, string serialNumber, string msgID, int zIndex, MQTTCVResponseStatus status)
		: base(serviceName, deviceName, eventName, serialNumber, msgID, zIndex, status)
	{
	}

	public MQTTFlowResponse(string serviceName, string deviceName, string eventName, string serialNumber, string msgID, int zIndex, int status, string msg)
		: base(serviceName, deviceName, eventName, serialNumber, msgID, zIndex, status, msg)
	{
	}

	public MQTTFlowResponse(string serviceName, string deviceName, string eventName, string serialNumber, string msgID, int zIndex, MQTTCVResponseStatus status, MQTTFlowRespResult data)
		: base(serviceName, deviceName, eventName, serialNumber, msgID, zIndex, status, data)
	{
	}

	public MQTTFlowResponse(string serviceName, string deviceName, string eventName, string serialNumber, string msgID, int zIndex, int status, string msg, MQTTFlowRespResult data)
		: base(serviceName, deviceName, eventName, serialNumber, msgID, zIndex, status, msg, data)
	{
	}

	public MQTTFlowResponse(string version, string serviceName, string deviceName, string eventName, string serialNumber, string msgID, int zIndex, int status, string desc, MQTTFlowRespResult data)
		: base(version, serviceName, deviceName, eventName, serialNumber, msgID, zIndex, status, desc, data)
	{
	}
}
