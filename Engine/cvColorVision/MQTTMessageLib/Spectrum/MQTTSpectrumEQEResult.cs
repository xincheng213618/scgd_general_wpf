namespace MQTTMessageLib.Spectrum;

public class MQTTSpectrumEQEResult
{
	public long TotalTime { get; set; }

	public int MasterId { get; set; }

	public int MasterResultType { get; set; }

	public MQTTSpectrumEQEResult(DeviceSpectrumEQEMeasureResponse response)
	{
		MasterId = response.MasterId;
		TotalTime = response.TotalTime;
		MasterResultType = 301;
	}
}
