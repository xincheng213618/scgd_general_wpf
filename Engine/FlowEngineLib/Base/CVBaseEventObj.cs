namespace FlowEngineLib.Base;

public class CVBaseEventObj
{
	public string EventName { get; set; }

	public object Data { get; set; }

	public CVBaseEventObj()
		: this(null, null)
	{
	}

	public CVBaseEventObj(string eventName, object data)
	{
		EventName = eventName;
		Data = data;
	}

	public CVBaseEventObj(string eventName)
		: this(eventName, null)
	{
	}
}
