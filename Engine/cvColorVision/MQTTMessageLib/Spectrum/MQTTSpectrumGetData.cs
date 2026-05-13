namespace MQTTMessageLib.Spectrum;

public class MQTTSpectrumGetData : MQTTCVBaseRequest<SpectrumMeasureParam>
{
	public MQTTSpectrumGetData()
		: base("GetData")
	{
	}

	public MQTTSpectrumGetData(string serviceName, SpectrumMeasureParam data)
		: this(serviceName, null, data)
	{
	}

	public MQTTSpectrumGetData(string serviceName, string serialNumber, SpectrumMeasureParam data)
		: base(serviceName, "GetData", serialNumber, data)
	{
	}
}
