namespace FlowEngineLib;

public class CommSensorData : TempCommSensorData
{
	public CVPhySensorCmd Cmd { get; set; }

	public CommSensorData(int tempId, string tempName, int cmdType, string cmdSend, string cmdReceive)
		: base(tempId, tempName)
	{
		if (cmdType != 0 && !string.IsNullOrEmpty(cmdSend))
		{
			Cmd = new CVPhySensorCmd
			{
				CmdType = (CommSensorCmdType)cmdType,
				Request = cmdSend,
				Response = cmdReceive,
				Delay = 0,
				RetryCount = 2,
				Timeout = 100
			};
		}
	}
}
