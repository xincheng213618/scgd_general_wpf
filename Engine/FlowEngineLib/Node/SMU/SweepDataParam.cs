namespace FlowEngineLib.Node.SMU;

public class SweepDataParam
{
	public SMUChannelType Channel { get; set; }

	public bool IsSourceV { get; set; }

	public float BeginValue { get; set; }

	public float EndValue { get; set; }

	public float LimitValue { get; set; }

	public int Points { get; set; }
}
