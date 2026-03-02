namespace FlowEngineLib;

public class CVPhySensorCmd
{
	public CommSensorCmdType CmdType { get; set; }

	public string Request { get; set; }

	public string Response { get; set; }

	public int RetryCount { get; set; }

	public int Delay { get; set; }

	public int Timeout { get; set; }
}
