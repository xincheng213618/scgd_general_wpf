using System.Collections.Generic;
using System.Linq;

namespace FlowEngineLib;

public class FlowNodeManager
{
	protected static FlowNodeManager _Instance;

	private Dictionary<string, List<DeviceNode>> NodeDevices;

	private Dictionary<string, List<DeviceNode>> AnonymousNodeDevices;

	public static FlowNodeManager Instance
	{
		get
		{
			if (_Instance == null)
			{
				_Instance = new FlowNodeManager();
			}
			return _Instance;
		}
	}

	public FlowNodeManager()
	{
		NodeDevices = new Dictionary<string, List<DeviceNode>>();
		AnonymousNodeDevices = new Dictionary<string, List<DeviceNode>>();
	}

	public void AddDevice(DeviceNode device)
	{
		string key = device.GetKey();
		if (string.IsNullOrEmpty(key))
		{
			key = device.DeviceType;
			if (!string.IsNullOrEmpty(key))
			{
				if (!AnonymousNodeDevices.ContainsKey(key))
				{
					AnonymousNodeDevices.Add(key, new List<DeviceNode>());
				}
				AnonymousNodeDevices[key].Add(device);
			}
		}
		else
		{
			if (!NodeDevices.ContainsKey(key))
			{
				NodeDevices.Add(key, new List<DeviceNode>());
			}
			NodeDevices[key].Add(device);
		}
	}

	public void UpdateDevice(Dictionary<string, Dictionary<string, DeviceNode>> devices)
	{
		foreach (KeyValuePair<string, List<DeviceNode>> anonymousNodeDevice in AnonymousNodeDevices)
		{
			if (!devices.ContainsKey(anonymousNodeDevice.Key) || devices[anonymousNodeDevice.Key].Count != 1)
			{
				continue;
			}
			DeviceNode value = devices[anonymousNodeDevice.Key].First().Value;
			foreach (DeviceNode item in anonymousNodeDevice.Value)
			{
				item.Update(value);
			}
		}
		foreach (KeyValuePair<string, Dictionary<string, DeviceNode>> device in devices)
		{
			foreach (KeyValuePair<string, DeviceNode> item2 in device.Value)
			{
				if (!NodeDevices.ContainsKey(item2.Key))
				{
					continue;
				}
				foreach (DeviceNode item3 in NodeDevices[item2.Key])
				{
					item3.Update(item2.Value);
				}
			}
		}
	}

	public void UpdateDevice(List<MQTTServiceInfo> services)
	{
		Dictionary<string, Dictionary<string, MQTTDeviceInfo>> dictionary = new Dictionary<string, Dictionary<string, MQTTDeviceInfo>>();
		foreach (MQTTServiceInfo service in services)
		{
			if (!dictionary.ContainsKey(service.ServiceType))
			{
				dictionary.Add(service.ServiceType, new Dictionary<string, MQTTDeviceInfo>());
			}
			foreach (KeyValuePair<string, MQTTDeviceInfo> device in service.Devices)
			{
				string key = new DeviceNode(string.Empty, device.Value.DeviceCode, new ServiceInfo(service.ServiceType, service.ServiceCode)).GetKey();
				if (!string.IsNullOrEmpty(key) && !dictionary[service.ServiceType].ContainsKey(key))
				{
					dictionary[service.ServiceType].Add(key, device.Value);
				}
			}
		}
		UpdateDevice(dictionary);
	}

	public void UpdateDevice(Dictionary<string, Dictionary<string, MQTTDeviceInfo>> devices)
	{
		foreach (KeyValuePair<string, List<DeviceNode>> anonymousNodeDevice in AnonymousNodeDevices)
		{
			if (!devices.ContainsKey(anonymousNodeDevice.Key) || devices[anonymousNodeDevice.Key].Count != 1)
			{
				continue;
			}
			MQTTDeviceInfo value = devices[anonymousNodeDevice.Key].First().Value;
			foreach (DeviceNode item in anonymousNodeDevice.Value)
			{
				item.Update(value);
			}
		}
		foreach (KeyValuePair<string, Dictionary<string, MQTTDeviceInfo>> device in devices)
		{
			foreach (KeyValuePair<string, MQTTDeviceInfo> item2 in device.Value)
			{
				if (!NodeDevices.ContainsKey(item2.Key))
				{
					continue;
				}
				foreach (DeviceNode item3 in NodeDevices[item2.Key])
				{
					item3.Update(item2.Value);
				}
			}
		}
	}

	public void ClearDevice()
	{
		AnonymousNodeDevices.Clear();
		NodeDevices.Clear();
	}
}
