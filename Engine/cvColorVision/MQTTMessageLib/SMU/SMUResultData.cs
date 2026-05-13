namespace MQTTMessageLib.SMU;

public class SMUResultData
{
	public double V { get; set; }

	public double I { get; set; }

	public SMUResultData()
		: this(0.0, 0.0)
	{
	}

	public SMUResultData(double v, double i)
	{
		V = v;
		I = i;
	}
}
