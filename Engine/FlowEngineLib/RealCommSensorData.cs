namespace FlowEngineLib;

public class RealCommSensorData
{
	public CVPhySensorCmd Cmd { get; set; }

	public RealCommSensorData(CommSensorCmdType cmdType, string cmdSend, string cmdReceive, int cmdTimeout, int retry, int delay)
	{
		Cmd = new CVPhySensorCmd
		{
			CmdType = cmdType,
			Request = cmdSend,
			Response = cmdReceive,
			Timeout = cmdTimeout,
			RetryCount = retry,
			Delay = delay
		};
	}
}
