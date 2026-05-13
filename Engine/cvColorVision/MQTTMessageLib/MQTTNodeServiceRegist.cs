using System;

namespace MQTTMessageLib;

public class MQTTNodeServiceRegist : MQTTNodeServiceHeader
{
	public string NodeAppId { get; set; }

	public string NodeKey { get; set; }

	public string NodeTopic { get; set; }

	public string SendTime { get; set; }

	public MQTTNodeServiceRegist(string version, string nodeName, string nodeAppId, string nodeKey, string nodeTopic, string serviceType)
		: base(version, nodeName, serviceType, "Regist")
	{
		NodeAppId = nodeAppId;
		NodeKey = nodeKey;
		NodeTopic = nodeTopic;
		SendTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
	}

	public MQTTNodeServiceRegist()
	{
	}

	public MQTTNodeServiceRegist(string nodeName, string nodeAppId, string nodeKey, string nodeTopic, string serviceType)
		: this("1.0", nodeName, nodeAppId, nodeKey, nodeTopic, serviceType)
	{
	}

	public MQTTNodeServiceRegist(MQTTServiceNode node)
		: this(node.NodeName, node.NodeAppId, node.NodeKey, node.NodeTopic, node.ServiceType.ToString())
	{
	}
}
