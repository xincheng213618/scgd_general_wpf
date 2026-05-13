using MQTTMessageLib.SMU;

namespace MQTTMessageLib.Spectrum;

public class SpectrumMeasureParam : SpectrumBaseMeasureParam
{
	public SMUMasterResultData SMUData { get; set; }
}
