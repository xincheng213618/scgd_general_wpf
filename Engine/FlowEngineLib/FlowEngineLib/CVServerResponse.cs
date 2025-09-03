namespace FlowEngineLib;

public class CVServerResponse
{
	public string Id;

	public ActionStatusEnum Status;

	public string Message;

	public string EventName;

	public dynamic Data;

	public CVServerResponse(string id, ActionStatusEnum status, string message, string eventName, dynamic data)
	{
		Id = id;
		Status = status;
		Message = message;
		EventName = eventName;
		Data = data;
	}
}
