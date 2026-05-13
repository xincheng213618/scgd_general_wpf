namespace MQTTMessageLib.Spectrum;

public class MQTTSpectrumOpen : MQTTCVBaseRequest<SpectrumOpenParam>
{
	public MQTTSpectrumOpen()
		: base("Open")
	{
	}

	public MQTTSpectrumOpen(string serviceName, SpectrumOpenParam data)
		: this(serviceName, null, data)
	{
	}

	public MQTTSpectrumOpen(string serviceName, string serialNumber, SpectrumOpenParam data)
		: base(serviceName, "Open", serialNumber, data)
	{
	}
}
