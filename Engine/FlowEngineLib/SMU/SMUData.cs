namespace FlowEngineLib.SMU;

public class SMUData
{
	public bool IsSourceV;

	public double MeasureValue;

	public double LimitValue;

	public SMUData(bool isSourceV, double measureValue, double limitValue)
	{
		IsSourceV = isSourceV;
		MeasureValue = measureValue;
		LimitValue = limitValue;
	}
}
