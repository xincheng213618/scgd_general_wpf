namespace MQTTMessageLib;

public class MQTTArchivedResponse
{
	public string Version { get; set; }

	public string EventName { get; set; }

	public string SerialNumber { get; set; }

	public int Code { get; set; }

	public string Message { get; set; }

	public MQTTArchivedResponse(MQTTArchivedRequest request, int code, string msg)
	{
		Version = request.Version;
		EventName = request.EventName;
		SerialNumber = request.SerialNumber;
		Code = code;
		Message = msg;
	}
}
