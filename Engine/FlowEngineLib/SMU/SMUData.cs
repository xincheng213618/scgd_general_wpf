namespace FlowEngineLib.SMU;

public class SMUData
{
	public SMUChannelType Channel { get; set; }

	public bool IsSourceV { get; set; }

	public double MeasureValue { get; set; }

	public double LimitValue { get; set; }

	public bool IsAutoRng { get; set; }

	public double SrcRng { get; set; }

	public double LmtRng { get; set; }

	public SMUData(SMUChannelType channel, bool isSourceV, double measureValue, double limitValue, bool isAutoRng, double srcRng, double lmtRng)
	{
		IsSourceV = isSourceV;
		Channel = channel;
		MeasureValue = measureValue;
		LimitValue = limitValue;
		IsAutoRng = isAutoRng;
		SrcRng = srcRng;
		LmtRng = lmtRng;
	}
}
