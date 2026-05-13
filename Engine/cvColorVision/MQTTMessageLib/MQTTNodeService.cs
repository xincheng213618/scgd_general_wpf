using System;
using System.Collections.Generic;

namespace MQTTMessageLib;

public class MQTTNodeService
{
	public int ServiceId { get; set; }

	public string ServiceToken { get; set; }

	public string ServiceCode { get; set; }

	public string ServiceName { get; set; }

	public string ServiceType { get; set; }

	public string UpChannel { get; set; }

	public string DownChannel { get; set; }

	public string LiveTime { get; set; }

	public int OverTime { get; set; }

	public Dictionary<string, MQTTNodeServiceDevice> Devices { get; set; }

	public MQTTNodeService()
	{
		Devices = new Dictionary<string, MQTTNodeServiceDevice>();
	}

	public MQTTNodeService(MQTTServiceHeartbeat hb)
		: this()
	{
		ServiceCode = hb.ServiceCode;
		ServiceType = hb.ServiceType;
		ServiceToken = hb.Token;
		UpChannel = hb.UpChannel;
		DownChannel = hb.DownChannel;
		LiveTime = hb.SendTime;
		OverTime = hb.OverTime;
		ServiceId = -1;
		foreach (DeviceHeartbeat device in hb.Devices)
		{
			Devices.Add(device.DeviceCode, new MQTTNodeServiceDevice
			{
				Code = device.DeviceCode,
				Status = device.DeviceStatus
			});
		}
	}

	public MQTTNodeService(int serviceId, string serviceType, string serviceCode, string serviceName)
		: this()
	{
		ServiceId = serviceId;
		ServiceType = serviceType;
		ServiceCode = serviceCode;
		ServiceName = serviceName;
	}

	public void Update(MQTTNodeService service)
	{
		UpChannel = service.UpChannel;
		DownChannel = service.DownChannel;
		LiveTime = service.LiveTime;
		OverTime = service.OverTime;
		ServiceToken = service.ServiceToken;
		foreach (KeyValuePair<string, MQTTNodeServiceDevice> device in service.Devices)
		{
			Devices[device.Key] = device.Value;
		}
	}

	public MQTTNodeServiceDevice GetDeviceByCode(string code)
	{
		if (Devices.ContainsKey(code))
		{
			return Devices[code];
		}
		return null;
	}

	public bool IsLive()
	{
		if (DateTime.TryParse(LiveTime, out var result))
		{
			if (OverTime > 0)
			{
				result.AddMilliseconds(OverTime);
			}
			if (result > DateTime.Now)
			{
				return true;
			}
		}
		return false;
	}
}
