using System;

namespace MQTTMessageLib;

public static class MQTTRCServiceTypeConst
{
	public static string BuildNodeName(string serviceType, string nodeName)
	{
		if (string.IsNullOrWhiteSpace(nodeName))
		{
			nodeName = Guid.NewGuid().ToString();
		}
		return serviceType + "." + nodeName;
	}

	public static string BuildNodeTopic(string nodeName, string rcName)
	{
		return string.Format("{0}/{1}/{2}", "MQTTRCService/Node", nodeName, rcName);
	}

	public static string BuildRegTopic(string nodeName)
	{
		return "MQTTRCService/Regist/" + nodeName;
	}

	public static string BuildHeartbeatTopic(string nodeName)
	{
		return "MQTTRCService/Heartbeat/" + nodeName;
	}

	public static string BuildPublicTopic(string nodeName)
	{
		return "MQTTRCService/Public/" + nodeName;
	}

	public static string BuildAdminTopic(string nodeName)
	{
		return "MQTTRCService/Admin/" + nodeName;
	}

	public static string BuildArchivedTopic(string nodeName)
	{
		return "MQTTRCService/Archived/" + nodeName;
	}

	public static string BuildSysConfigTopic(string nodeName)
	{
		return "SysRes/config/" + nodeName;
	}

	public static string BuildSysConfigRespTopic(string nodeName)
	{
		return "SysRes/config/Resp/" + nodeName;
	}
}
