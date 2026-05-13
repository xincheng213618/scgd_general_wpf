namespace MQTTMessageLib.Spectrum;

public class MQTTSpectrumResult
{
	public long TotalTime { get; set; }

	public float IntegralTime { get; set; }

	public int MasterId { get; set; }

	public int MasterResultType { get; set; }

	public MQTTSpectrumResult(DeviceSpectrumMeasureResponse response)
	{
		MasterId = response.MasterId;
		TotalTime = response.TotalTime;
		IntegralTime = response.Data.IntegralTime;
		MasterResultType = 300;
	}
}
