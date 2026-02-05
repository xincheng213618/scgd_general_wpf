namespace FlowEngineLib.Node.SMU;

public class SMUGetMeasureResultParam
{
	public int WaitTime { get; set; }

	public SMUGetMeasureResultParam(int waitTime)
	{
		WaitTime = waitTime;
	}
}
