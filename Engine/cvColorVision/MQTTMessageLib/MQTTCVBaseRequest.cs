using System;
using Newtonsoft.Json;

namespace MQTTMessageLib;

public class MQTTCVBaseRequest<T> : MQTTCVRequestTokenHeader
{
	[JsonProperty("params")]
	public T Data { get; set; }

	public MQTTCVBaseRequest()
		: this((string)null)
	{
	}

	public MQTTCVBaseRequest(string eventName)
		: this((string)null, (string)null, eventName)
	{
	}

	public MQTTCVBaseRequest(string serviceName, string deviceName, string eventName)
		: this(serviceName, deviceName, eventName, default(T))
	{
	}

	public MQTTCVBaseRequest(string serviceName, string deviceName, string eventName, T data)
		: this(serviceName, deviceName, eventName, string.Empty, string.Empty, data)
	{
	}

	public MQTTCVBaseRequest(string serviceName, string deviceName, string eventName, string serialNumber, T data)
		: this(serviceName, deviceName, eventName, serialNumber, string.Empty, data)
	{
	}

	public MQTTCVBaseRequest(string serviceName, string deviceName, string eventName, string serialNumber, string token, T data)
		: this("1.0", serviceName, deviceName, eventName, serialNumber, Guid.NewGuid().ToString("N"), token, data, -1)
	{
	}

	public MQTTCVBaseRequest(string version, string serviceName, string deviceName, string eventName, string serialNumber, string msgID, string token, T data, int zIndex = -1)
		: base(version, serviceName, deviceName, eventName, serialNumber, msgID, token, zIndex)
	{
		Data = data;
	}
}
