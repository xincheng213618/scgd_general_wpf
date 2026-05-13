using System;

namespace MQTTMessageLib;

public class MQTTNodeServiceHeader
{
	public string Version { get; set; }

	public string MsgId { get; set; }

	public string NodeName { get; set; }

	public string ServiceType { get; set; }

	public string EventName { get; set; }

	public MQTTNodeServiceHeader()
	{
		Version = "1.0";
		MsgId = Guid.NewGuid().ToString();
	}

	public MQTTNodeServiceHeader(string nodeName, string serviceType, string eventName)
		: this("1.0", nodeName, serviceType, eventName)
	{
	}

	public MQTTNodeServiceHeader(string version, string nodeName, string serviceType, string eventName)
	{
		NodeName = nodeName;
		ServiceType = serviceType;
		EventName = eventName;
		Version = version;
		MsgId = Guid.NewGuid().ToString();
	}
}
