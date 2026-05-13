namespace MQTTMessageLib.Spectrum;

public class MQTTSpectrumClose : MQTTCVRequestTokenHeader
{
	public MQTTSpectrumClose()
		: this(string.Empty)
	{
	}

	public MQTTSpectrumClose(string serviceName)
		: this(serviceName, string.Empty)
	{
	}

	public MQTTSpectrumClose(string serviceName, string serialNumber)
		: base(serviceName, string.Empty, "Close", serialNumber)
	{
	}
}
