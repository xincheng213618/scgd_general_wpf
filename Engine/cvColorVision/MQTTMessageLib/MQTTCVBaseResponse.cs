namespace MQTTMessageLib;

public class MQTTCVBaseResponse<T> : MQTTCVResponseHeader
{
	public T Data { get; set; }

	public MQTTCVBaseResponse()
		: this(new MQTTCVRequestHeader(), new MQTTCVResponseStatus())
	{
	}

	public MQTTCVBaseResponse(MQTTCVRequestHeader request, MQTTCVResponseStatus status)
		: base(request, status.Code, status.Desc)
	{
	}

	public MQTTCVBaseResponse(MQTTCVRequestHeader request, MQTTCVResponseStatus status, T data)
		: base(request, status.Code, status.Desc)
	{
		Data = data;
	}

	public MQTTCVBaseResponse(string serviceName, string deviceName, string eventName, string serialNumber, string msgID, int zIndex, MQTTCVResponseStatus status)
		: this(serviceName, deviceName, eventName, serialNumber, msgID, zIndex, status.Code, status.Desc)
	{
	}

	public MQTTCVBaseResponse(string serviceName, string deviceName, string eventName, string serialNumber, string msgID, int zIndex, int status, string msg)
		: this(serviceName, deviceName, eventName, serialNumber, msgID, zIndex, status, msg, default(T))
	{
	}

	public MQTTCVBaseResponse(string serviceName, string deviceName, string eventName, string serialNumber, string msgID, int zIndex, MQTTCVResponseStatus status, T data)
		: this("1.0", serviceName, deviceName, eventName, serialNumber, msgID, zIndex, status.Code, status.Desc, data)
	{
	}

	public MQTTCVBaseResponse(string serviceName, string deviceName, string eventName, string serialNumber, string msgID, int zIndex, int status, string msg, T data)
		: this("1.0", serviceName, deviceName, eventName, serialNumber, msgID, zIndex, status, msg, data)
	{
	}

	public MQTTCVBaseResponse(string version, string serviceName, string deviceName, string eventName, string serialNumber, string msgID, int zIndex, int status, string desc, T data)
		: base(version, serviceName, deviceName, eventName, serialNumber, msgID, zIndex, status, desc)
	{
		Data = data;
	}
}
