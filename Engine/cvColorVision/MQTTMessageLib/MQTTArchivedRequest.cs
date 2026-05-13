namespace MQTTMessageLib;

public class MQTTArchivedRequest
{
	public string Version { get; set; }

	public string EventName { get; set; }

	public string SerialNumber { get; set; }

	public string ResponseTopic { get; set; }

	public MQTTArchivedRequest()
	{
	}

	public MQTTArchivedRequest(string serialNumber)
		: this(serialNumber, string.Empty)
	{
	}

	public MQTTArchivedRequest(string serialNumber, string responseTopic)
		: this("1.0", "Archived", serialNumber, responseTopic)
	{
	}

	public MQTTArchivedRequest(string version, string eventName, string serialNumber, string responseTopic)
	{
		Version = version;
		EventName = eventName;
		SerialNumber = serialNumber;
		ResponseTopic = responseTopic;
	}
}
