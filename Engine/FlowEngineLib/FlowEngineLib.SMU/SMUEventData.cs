namespace FlowEngineLib.SMU;

public class SMUEventData
{
	public string SerialNumber;

	public string SourceType;

	public double MeasureValue;

	public double LimitValue;

	public SMUEventData(string source, double measureValue, double limitValue)
		: this("", source, measureValue, limitValue)
	{
	}

	public SMUEventData(string sn, string source, double measureValue, double limitValue)
	{
		SerialNumber = sn;
		SourceType = source;
		MeasureValue = measureValue;
		LimitValue = limitValue;
	}
}
