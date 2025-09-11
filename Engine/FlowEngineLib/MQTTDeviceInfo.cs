namespace FlowEngineLib;

public class MQTTDeviceInfo
{
	public string ID { get; set; }

	public string DeviceCode { get; set; }

	public MQTTServiceInfo Service { get; set; }
}
