namespace MQTTMessageLib.Camera;

public class MasterResponseResult : MasterResult
{
	public long TotalTime { get; set; }

	public MasterResponseResult(long totalTime)
		: this(-1, totalTime)
	{
	}

	public MasterResponseResult(int masterId, long totalTime)
		: this(masterId, -1, totalTime)
	{
	}

	public MasterResponseResult(int masterId, int masterResultType, long totalTime)
		: base(masterId, masterResultType)
	{
		TotalTime = totalTime;
	}
}
