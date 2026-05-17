using System;
using System.Collections.Generic;
using CVCommCore;
using MQTTMessageLib.Util;

namespace MQTTMessageLib;

public class MQTTServiceNode
{
	public string NodeName { get; set; }

	public string NodeKey { get; set; }

	public string NodeAppId { get; set; }

	public string NodeTopic { get; set; }

	public CVServiceType ServiceType { get; set; }

	public bool AutoReg { get; set; }

	public int HeartbeatTime { get; set; }

	public bool NHBIsDebugOut { get; set; }

	public ServiceNodeStatus Status { get; set; }

	public Dictionary<string, MQTTNodeService> Services { get; set; }

	public NodeToken Token { get; set; }

	public MQTTServiceNode()
	{
	}

	public MQTTServiceNode(MQTTNodeServiceRegist reg, NodeToken token)
	{
		NodeName = reg.NodeName;
		ServiceType = Enum.Parse<CVServiceType>(reg.ServiceType, ignoreCase: true);
		NodeAppId = reg.NodeAppId;
		NodeKey = reg.NodeKey;
		NodeTopic = reg.NodeTopic;
		Token = token;
		HeartbeatTime = 5000;
		NHBIsDebugOut = false;
		Status = ServiceNodeStatus.Unregistered;
		Services = new Dictionary<string, MQTTNodeService>();
		AutoReg = true;
	}

	public MQTTServiceNode(string RCNodeName, string nodeName, string serviceType, string nodeAppId, string nodeKey, int heartbeatTime, bool NHBIsDebugOut)
	{
		NodeName = MQTTRCServiceTypeConst.BuildNodeName(serviceType, nodeName);
		ServiceType = Enum.Parse<CVServiceType>(serviceType, ignoreCase: true);
		NodeAppId = nodeAppId;
		NodeKey = nodeKey;
		NodeTopic = MQTTRCServiceTypeConst.BuildNodeTopic(NodeName, RCNodeName);
		Token = null;
		HeartbeatTime = heartbeatTime;
		this.NHBIsDebugOut = NHBIsDebugOut;
		Status = ServiceNodeStatus.Unregistered;
		Services = new Dictionary<string, MQTTNodeService>();
		AutoReg = true;
	}

	public void UpdateRegInfo(MQTTNodeServiceRegist reg, int tokenExpires)
	{
		NodeTopic = reg.NodeTopic;
		if (IsRegistered())
		{
			if (Token.IsExpired())
			{
				Token.Refresh(tokenExpires);
			}
		}
		else
		{
			if (Token == null)
			{
				Token = new NodeToken(tokenExpires);
			}
			else
			{
				Token.Refresh();
			}
			Status = ServiceNodeStatus.Registered;
		}
		if (!AutoReg)
		{
			return;
		}
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, MQTTNodeService> service in Services)
		{
			if (!service.Value.IsLive())
			{
				list.Add(service.Key);
			}
		}
		foreach (string item in list)
		{
			Services.Remove(item);
		}
	}

	public bool IsRegistered()
	{
		return Status == ServiceNodeStatus.Registered;
	}

	public bool CheckNodeToken(string token)
	{
		if (Token != null && Token.AccessToken.Equals(token))
		{
			return true;
		}
		return false;
	}

	public bool CheckNode(string nodeName, string nodeAppId, string nodeKey)
	{
		if (NodeName.Equals(nodeName) && NodeAppId.Equals(nodeAppId))
		{
			return NodeKey.Equals(nodeKey);
		}
		return false;
	}

	public void UpdateOrAddService(MQTTNodeService service)
	{
		if (Services.ContainsKey(service.ServiceCode))
		{
			Services[service.ServiceCode].Update(service);
		}
		else
		{
			Services[service.ServiceCode] = service;
		}
	}

	public void GetServices(Dictionary<CVServiceType, List<MQTTNodeService>> services)
	{
		foreach (KeyValuePair<string, MQTTNodeService> service in Services)
		{
			CVServiceType key = EnumTool.ParseEnum<CVServiceType>(service.Value.ServiceType);
			if (!services.ContainsKey(key))
			{
				services.Add(key, new List<MQTTNodeService>());
			}
			services[key].Add(service.Value);
		}
	}
}
