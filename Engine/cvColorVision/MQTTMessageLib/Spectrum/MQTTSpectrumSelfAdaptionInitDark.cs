namespace MQTTMessageLib.Spectrum;

public class MQTTSpectrumSelfAdaptionInitDark : MQTTCVBaseRequest<SpectrumSelfAdaptionInitDark>
{
	public MQTTSpectrumSelfAdaptionInitDark()
		: base("InitAutoDark")
	{
	}

	public MQTTSpectrumSelfAdaptionInitDark(string serviceName, SpectrumSelfAdaptionInitDark data)
		: this(serviceName, null, data)
	{
	}

	public MQTTSpectrumSelfAdaptionInitDark(string serviceName, string serialNumber, SpectrumSelfAdaptionInitDark data)
		: base(serviceName, "InitAutoDark", serialNumber, data)
	{
	}
}
