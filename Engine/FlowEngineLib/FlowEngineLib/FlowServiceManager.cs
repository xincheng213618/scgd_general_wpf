using System.Collections.Generic;

namespace FlowEngineLib;

public class FlowServiceManager
{
	protected static FlowServiceManager _Instance;

	private Dictionary<string, ServiceNode> Services;

	public static FlowServiceManager Instance
	{
		get
		{
			if (_Instance == null)
			{
				_Instance = new FlowServiceManager();
			}
			return _Instance;
		}
	}

	public FlowServiceManager()
	{
		Services = new Dictionary<string, ServiceNode>();
	}

	public void AddMQTTService(MQTTServiceInfo service)
	{
		if (!Services.ContainsKey(service.ServiceType))
		{
			Services[service.ServiceType] = new ServiceNode(service.ServiceType);
		}
		Services[service.ServiceType].AddMQTTService(service);
	}

	public void AddMQTTService(List<MQTTServiceInfo> services)
	{
		Services.Clear();
		foreach (MQTTServiceInfo service in services)
		{
			AddMQTTService(service);
		}
	}

	public MQTTServiceInfo GetService(string sType, string serviceCode)
	{
		if (Services.ContainsKey(sType))
		{
			return Services[sType].GetService(serviceCode);
		}
		return null;
	}

	public void Clear()
	{
		Services.Clear();
	}
}
