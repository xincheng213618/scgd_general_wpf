namespace FlowEngineLib;

public class SMUResultData
{
	public SMUChannelType Channel { get; set; }

	public double V { get; set; }

	public double I { get; set; }

	public int MasterId { get; set; }

	public int MasterResultType { get; set; }

	public SMUResultData(SMUChannelType channel, double v, double i, int masterId, int masterResultType)
	{
		Channel = channel;
		V = v;
		I = i;
		MasterId = masterId;
		MasterResultType = masterResultType;
	}

	public SMUResultData()
	{
	}
}
