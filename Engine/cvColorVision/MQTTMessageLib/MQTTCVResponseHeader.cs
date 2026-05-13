namespace MQTTMessageLib;

public class MQTTCVResponseHeader : MQTTCVRequestHeader
{
	public int Code { get; set; }

	public string Message { get; set; }

	public MQTTCVResponseHeader(MQTTCVRequestHeader request, int status, string desc)
		: this(request.Version, request.ServiceName, request.DeviceCode, request.EventName, request.SerialNumber, request.MsgID, request.ZIndex, status, desc)
	{
	}

	public MQTTCVResponseHeader(string version, string serviceName, string deviceName, string eventName, string serialNumber, string msgID, int zIndex, int status, string desc)
		: base(version, serviceName, deviceName, eventName, serialNumber, msgID, zIndex)
	{
		Code = status;
		Message = desc;
	}
}
