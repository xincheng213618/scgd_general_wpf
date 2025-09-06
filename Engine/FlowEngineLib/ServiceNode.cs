using System.Collections.Generic;
using FlowEngineLib.Base;

namespace FlowEngineLib;

public class ServiceNode
{
	public string ServiceType { get; }

	public Dictionary<string, MQTTServiceInfo> MQTTServices { get; }

	public ServiceNode(string sType)
	{
		ServiceType = sType;
		MQTTServices = new Dictionary<string, MQTTServiceInfo>();
	}

	public void AddMQTTService(MQTTServiceInfo service)
	{
		string serviceCode = service.ServiceCode;
		if (!string.IsNullOrEmpty(serviceCode) && !MQTTServices.ContainsKey(serviceCode))
		{
			MQTTServices.Add(serviceCode, service);
		}
	}

	public MQTTServiceInfo GetService(string serviceCode)
	{
		if (MQTTServices.ContainsKey(serviceCode))
		{
			return MQTTServices[serviceCode];
		}
		return null;
	}

	public void AddMQTTService(CVBaseServerNode service)
	{
		MQTTServiceInfo service2 = new MQTTServiceInfo
		{
			ServiceType = service.NodeType,
			ServiceCode = service.NodeName,
			PublishTopic = service.DefaultPublishTopic,
			SubscribeTopic = service.DefaultSubscribeTopic
		};
		AddMQTTService(service2);
	}
}
