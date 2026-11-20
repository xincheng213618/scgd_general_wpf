namespace FlowEngineLib.Base;

public class CVBaseEventCmd
{
	private volatile PendingRemoteFlowResult remoteResult;

	public CVMQTTRequest cmd { get; private set; }

	public CVServerResponse resp { get; set; }
	public string ReplyTopic { get; internal set; }
	internal PendingRemoteFlowResult RemoteResult
	{
		get => remoteResult;
		set => remoteResult = value;
	}

	public LockFreeMessageWaiter waiter { get; private set; }

	public CVBaseEventCmd(
		CVMQTTRequest cmd,
		CVServerResponse resp)
	{
		this.cmd = cmd;
		this.resp = resp;
		waiter = new LockFreeMessageWaiter();
	}
}
