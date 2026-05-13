namespace MQTTMessageLib.Spectrum;

public class MQTTSpectrumGetParam : MQTTCVRequestTokenHeader
{
	public MQTTSpectrumGetParam()
		: this(string.Empty)
	{
	}

	public MQTTSpectrumGetParam(string serviceName)
		: this(serviceName, string.Empty)
	{
	}

	public MQTTSpectrumGetParam(string serviceName, string serialNumber)
		: base(serviceName, string.Empty, "GetParam", serialNumber)
	{
	}
}
