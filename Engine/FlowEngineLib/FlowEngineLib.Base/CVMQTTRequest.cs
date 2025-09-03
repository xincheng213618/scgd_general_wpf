using System;
using Newtonsoft.Json;

namespace FlowEngineLib.Base;

public class CVMQTTRequest
{
	public string Version { get; set; }

	[JsonProperty("ServiceName")]
	public string ServiceCode { get; set; }

	public string DeviceCode { get; set; }

	public int ZIndex { get; set; }

	public string Token { get; set; }

	public string EventName { get; set; }

	public string SerialNumber { get; set; }

	public string MsgID { get; set; }

	[JsonProperty("params")]
	public object Data { get; set; }

	[JsonIgnore]
	public long SendTime { get; set; }

	public CVMQTTRequest()
		: this(string.Empty, string.Empty, string.Empty, string.Empty)
	{
	}

	public CVMQTTRequest(MQTTServiceInfo service, string deviceCode, string eventName, string sn, object data)
		: this(service.ServiceCode, deviceCode, eventName, sn, data, service.Token)
	{
	}

	public CVMQTTRequest(string serviceCode, string deviceCode, string eventName)
		: this(serviceCode, deviceCode, eventName, null, string.Empty)
	{
	}

	public CVMQTTRequest(string serviceCode, string deviceCode, string eventName, string token)
		: this(serviceCode, deviceCode, eventName, null, token)
	{
	}

	public CVMQTTRequest(string serviceCode, string deviceCode, string eventName, string sn, string token)
		: this(serviceCode, deviceCode, eventName, sn, null, token)
	{
	}

	public CVMQTTRequest(string serviceCode, string deviceCode, string eventName, string sn, object data, string token)
		: this("1.1", serviceCode, deviceCode, eventName, sn, data, token)
	{
	}

	public CVMQTTRequest(string version, string serviceCode, string deviceCode, string eventName, string sn, object data, string token)
	{
		MsgID = Guid.NewGuid().ToString();
		SendTime = DateTime.Now.Ticks;
		ServiceCode = serviceCode;
		DeviceCode = deviceCode;
		Version = version;
		EventName = eventName;
		SerialNumber = sn;
		Token = token;
		Data = data;
	}
}
