namespace MQTTMessageLib.Spectrum;

public class MQTTSpectrumSetParam : MQTTCVBaseRequest<SpectrumSysParam>
{
	public MQTTSpectrumSetParam()
		: base("SetParam")
	{
	}

	public MQTTSpectrumSetParam(string serviceName, SpectrumSysParam data)
		: this(serviceName, null, data)
	{
	}

	public MQTTSpectrumSetParam(string serviceName, string serialNumber, SpectrumSysParam data)
		: base(serviceName, "SetParam", serialNumber, data)
	{
	}
}
