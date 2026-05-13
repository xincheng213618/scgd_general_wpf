namespace MQTTMessageLib.Spectrum;

public class MQTTSpectrumGetDataAutoStart : MQTTCVBaseRequest<SpectrumMeasureParam>
{
	public MQTTSpectrumGetDataAutoStart()
		: base("GetDataAuto")
	{
	}

	public MQTTSpectrumGetDataAutoStart(string serviceName, SpectrumMeasureParam data)
		: this(serviceName, null, data)
	{
	}

	public MQTTSpectrumGetDataAutoStart(string serviceName, string serialNumber, SpectrumMeasureParam data)
		: base(serviceName, "GetDataAuto", serialNumber, data)
	{
	}
}
