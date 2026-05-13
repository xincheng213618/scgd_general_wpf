using System;
using System.Collections.Generic;

namespace MQTTMessageLib;

public class MQTTDeviceStatusChanged
{
	public string Version { get; set; }

	public string ServiceName { get; set; }

	public string EventName { get; set; }

	public string MsgID { get; set; }

	public int Code { get; set; }

	public List<DeviceHeartbeat> Data { get; set; }

	public MQTTDeviceStatusChanged()
		: this(null)
	{
	}

	public MQTTDeviceStatusChanged(string serviceName)
	{
		Code = 0;
		ServiceName = serviceName;
		Version = "1.0";
		EventName = "Heartbeat";
		MsgID = Guid.NewGuid().ToString();
	}

	public MQTTDeviceStatusChanged(string serviceName, List<DeviceHeartbeat> deviceStatues)
		: this(serviceName)
	{
		Data = deviceStatues;
	}
}
