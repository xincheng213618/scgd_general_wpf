namespace MQTTMessageLib.PG;

public class MQTTPGOpen : MQTTCVRequestTokenHeader
{
	public MQTTPGOpen(string serviceName, string serialNumber)
		: base(serviceName, "Open", serialNumber)
	{
	}
}
