namespace MQTTMessageLib;

public class MQTTNodeServiceStartupRequest : MQTTNodeServiceHeader
{
	public MQTTServiceNode Data { get; set; }

	public MQTTNodeServiceStartupRequest(MQTTServiceNode data)
		: base(data.NodeName, data.ServiceType.ToString(), "Startup")
	{
		Data = data;
	}

	public MQTTNodeServiceStartupRequest()
	{
	}
}
