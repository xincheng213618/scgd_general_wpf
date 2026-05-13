namespace MQTTMessageLib.Spectrum;

public class MQTTSpectrumInitDark : MQTTCVBaseRequest<SpectrumInitDarkParam>
{
	public MQTTSpectrumInitDark()
		: base("InitDark")
	{
	}

	public MQTTSpectrumInitDark(string serviceName, SpectrumInitDarkParam data)
		: this(serviceName, null, data)
	{
	}

	public MQTTSpectrumInitDark(string serviceName, string serialNumber, SpectrumInitDarkParam data)
		: base(serviceName, "InitDark", serialNumber, data)
	{
	}
}
