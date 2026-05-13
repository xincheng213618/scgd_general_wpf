namespace MQTTMessageLib.Spectrum;

public class MQTTSpectrumGetDataAutoStop : MQTTCVRequestTokenHeader
{
	public MQTTSpectrumGetDataAutoStop()
		: this(string.Empty)
	{
	}

	public MQTTSpectrumGetDataAutoStop(string serviceName)
		: this(serviceName, string.Empty)
	{
	}

	public MQTTSpectrumGetDataAutoStop(string serviceName, string serialNumber)
		: base(serviceName, string.Empty, "GetDataAutoStop", serialNumber)
	{
	}
}
