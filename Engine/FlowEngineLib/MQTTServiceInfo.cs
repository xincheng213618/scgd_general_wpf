using System.Collections.Generic;

namespace FlowEngineLib;

public class MQTTServiceInfo
{
	public string ServiceType { get; set; }

	public string ServiceCode { get; set; }

	public string SubscribeTopic { get; set; }

	public string PublishTopic { get; set; }

	public string Token { get; set; }

	public Dictionary<string, MQTTDeviceInfo> Devices { get; }

	public MQTTServiceInfo()
	{
		Devices = new Dictionary<string, MQTTDeviceInfo>();
	}

	public void AddDevice(string id, string code)
	{
		if (!Devices.ContainsKey(code))
		{
			Devices.Add(code, new MQTTDeviceInfo
			{
				ID = id,
				DeviceCode = code,
				Service = this
			});
		}
	}
}
