namespace MQTTMessageLib.RC;

public class MQTTRCServicesRestartRequest : MQTTNodeServiceTokenHeader
{
	public string AppId { get; set; }

	public string ServiceCode { get; set; }

	public string DeviceCode { get; set; }

	public MQTTRCServicesRestartRequest(string appId, string nodeName, string serviceType, string token, string serviceCode, string deviceCode)
		: base(nodeName, serviceType, "ResetAppNodes", token)
	{
		AppId = appId;
		ServiceCode = serviceCode;
		DeviceCode = deviceCode;
	}

	public MQTTRCServicesRestartRequest(string appId, string nodeName, string serviceType, string token, string serviceCode)
		: this(appId, nodeName, serviceType, token, serviceCode, string.Empty)
	{
	}

	public MQTTRCServicesRestartRequest(string appId, string nodeName, string serviceType, string token)
		: this(appId, nodeName, serviceType, token, string.Empty, string.Empty)
	{
	}

	public MQTTRCServicesRestartRequest()
	{
	}
}
