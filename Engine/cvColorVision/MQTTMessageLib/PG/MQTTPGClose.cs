namespace MQTTMessageLib.PG;

public class MQTTPGClose : MQTTCVRequestTokenHeader
{
	public MQTTPGClose(string serviceName, string serialNumber)
		: base(serviceName, "Close", serialNumber)
	{
	}
}
