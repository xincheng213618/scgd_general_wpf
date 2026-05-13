using System;

namespace MQTTMessageLib;

public class MQTTRCServiceTypeConst
{
	public const string RCServiceType = "MQTTRCService";

	public const string RCRegTopic = "MQTTRCService/Regist";

	public const string RCHeartbeatTopic = "MQTTRCService/Heartbeat";

	public const string RCPublicTopic = "MQTTRCService/Public";

	public const string RCAdminTopic = "MQTTRCService/Admin";

	public const string RCNodeTopic = "MQTTRCService/Node";

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

	public static string BuildNodeTopic(string nodeName)
	{
		return string.Format("{0}/{1}", "MQTTRCService/Node", nodeName);
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

	public static string BuildFlowTopic(string nodeName)
	{
		return "MQTTRCService/Flow/" + nodeName;
	}

	public static string BuildArchivedTopic(string nodeName)
	{
		return "MQTTRCService/Archived/" + nodeName;
	}

	public static string BuildServiceUpTopic(string serviceType, string serviceName, string rcName)
	{
		return string.Format("{2}/{0}/{1}/CMD", serviceType, serviceName, rcName);
	}

	public static string BuildServiceDownTopic(string serviceType, string serviceName, string rcName)
	{
		return string.Format("{2}/{0}/{1}/STATUS", serviceType, serviceName, rcName);
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
