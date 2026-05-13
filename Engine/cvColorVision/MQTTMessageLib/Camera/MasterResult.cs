namespace MQTTMessageLib.Camera;

public class MasterResult
{
	public int MasterId { get; set; }

	public int MasterResultType { get; set; }

	public string MasterValue { get; set; }

	public MasterResult()
		: this(-1, -1)
	{
	}

	public MasterResult(int masterId, int masterResultType)
	{
		MasterId = masterId;
		MasterResultType = masterResultType;
	}
}
