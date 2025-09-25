namespace FlowEngineLib;

public class SMUResultData
{
	public double V { get; set; }

	public double I { get; set; }

	public int MasterId { get; set; }

	public int MasterResultType { get; set; }

	public SMUResultData(double v, double i, int masterId, int masterResultType)
	{
		V = v;
		I = i;
		MasterId = masterId;
		MasterResultType = masterResultType;
	}

	public SMUResultData()
	{
	}
}
