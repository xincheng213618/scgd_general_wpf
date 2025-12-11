namespace FlowEngineLib.SMU;

public class SMUData
{
	public SMUChannelType Channel { get; set; }

	public bool IsSourceV { get; set; }

	public double MeasureValue { get; set; }

	public double LimitValue { get; set; }

	public SMUData(bool isSourceV, SMUChannelType channel, double measureValue, double limitValue)
	{
		IsSourceV = isSourceV;
		Channel = channel;
		MeasureValue = measureValue;
		LimitValue = limitValue;
	}
}
