using System;
using System.Collections.Generic;

namespace MQTTMessageLib;

public class MQTTServiceHeartbeat : MQTTNodeServiceTokenHeader
{
	public string ServiceCode { get; set; }

	public string UpChannel { get; set; }

	public string DownChannel { get; set; }

	public string SendTime { get; set; }

	public int OverTime { get; set; }

	public List<DeviceHeartbeat> Devices { get; set; }

	public MQTTServiceHeartbeat()
	{
	}

	public MQTTServiceHeartbeat(string nodeName, string upChannel, string downChannel, string serviceType, string serviceCode, List<DeviceHeartbeat> deviceHbs, string token, int overTime)
		: this("1.1", nodeName, upChannel, downChannel, serviceType, serviceCode, deviceHbs, token, overTime)
	{
	}

	public MQTTServiceHeartbeat(string version, string nodeName, string upChannel, string downChannel, string serviceType, string serviceCode, List<DeviceHeartbeat> deviceHbs, string token, int overTime)
		: base(version, nodeName, serviceType, "ServiceHeartbeat")
	{
		base.Version = version;
		base.NodeName = nodeName;
		UpChannel = upChannel;
		DownChannel = downChannel;
		ServiceCode = serviceCode;
		Devices = deviceHbs;
		base.Token = token;
		base.ServiceType = serviceType;
		base.EventName = "ServiceHeartbeat";
		SendTime = DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss");
		base.MsgId = Guid.NewGuid().ToString();
		OverTime = overTime;
	}
}
