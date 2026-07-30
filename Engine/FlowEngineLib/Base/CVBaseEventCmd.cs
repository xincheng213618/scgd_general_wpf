namespace FlowEngineLib.Base;

public class CVBaseEventCmd
{
	public CVMQTTRequest cmd { get; private set; }

	public CVServerResponse resp { get; set; }

	public LockFreeMessageWaiter waiter { get; private set; }

	public int AttemptNumber { get; }

	public CVBaseEventCmd(
		CVMQTTRequest cmd,
		CVServerResponse resp)
		: this(cmd, resp, 1)
	{
	}

	internal CVBaseEventCmd(
		CVMQTTRequest cmd,
		CVServerResponse resp,
		int attemptNumber)
	{
		this.cmd = cmd;
		this.resp = resp;
		AttemptNumber = attemptNumber < 1
			? 1
			: attemptNumber;
		waiter = new LockFreeMessageWaiter();
	}
}
