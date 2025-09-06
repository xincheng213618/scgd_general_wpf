namespace FlowEngineLib.Base;

public class CVBaseEventCmd
{
	public CVMQTTRequest cmd { get; private set; }

	public CVServerResponse resp { get; set; }

	public LockFreeMessageWaiter waiter { get; private set; }

	public CVBaseEventCmd(CVMQTTRequest cmd, CVServerResponse resp)
	{
		this.cmd = cmd;
		this.resp = resp;
		//waiter = new LockFreeMessageWaiter();
	}
}
