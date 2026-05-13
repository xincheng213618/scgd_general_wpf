namespace MQTTMessageLib.PG;

public class MQTTPGCustom : MQTTCVBaseRequest<PGRequestCustomParam>
{
	public MQTTPGCustom(string serviceName, string serialNumber, PGRequestCustomParam data)
		: base(serviceName, "Custom", serialNumber, data)
	{
	}
}
