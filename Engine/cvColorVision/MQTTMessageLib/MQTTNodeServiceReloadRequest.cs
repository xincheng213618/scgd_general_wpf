namespace MQTTMessageLib;

public class MQTTNodeServiceReloadRequest : MQTTNodeServiceHeader
{
	public string ServiceCode { get; set; }

	public string DeviceCode { get; set; }

	public MQTTNodeServiceReloadRequest(string nodeName, string serviceCode, string serviceType, string deviceCode)
		: base(nodeName, serviceType, "ReloadService")
	{
		ServiceCode = serviceCode;
		DeviceCode = deviceCode;
	}
}
