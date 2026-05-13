using System;

namespace MQTTMessageLib;

public class MQTTCVRequestTokenHeader : MQTTCVRequestHeader
{
	public string Token { get; set; }

	public MQTTCVRequestTokenHeader(string serviceName, string deviceName, string eventName)
		: this(serviceName, deviceName, eventName, string.Empty)
	{
	}

	public MQTTCVRequestTokenHeader(string serviceName, string deviceName, string eventName, string serialNumber)
		: this(serviceName, deviceName, eventName, serialNumber, string.Empty)
	{
	}

	public MQTTCVRequestTokenHeader(string serviceName, string deviceName, string eventName, string serialNumber, string token)
		: this("1.0", serviceName, deviceName, eventName, serialNumber, Guid.NewGuid().ToString("N"), token)
	{
	}

	public MQTTCVRequestTokenHeader(string version, string serviceName, string deviceName, string eventName, string serialNumber, string msgID, string token, int zIndex = -1)
		: base(version, serviceName, deviceName, eventName, serialNumber, msgID, zIndex)
	{
		Token = token;
	}

	public MQTTCVRequestTokenHeader()
		: this(string.Empty, string.Empty, string.Empty)
	{
	}

	public bool IsTokenValid(string accessToken)
	{
		_ = base.Version;
		if (!string.IsNullOrWhiteSpace(Token))
		{
			return Token.Equals(accessToken);
		}
		return false;
	}
}
