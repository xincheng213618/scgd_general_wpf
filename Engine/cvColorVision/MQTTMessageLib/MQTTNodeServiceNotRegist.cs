namespace MQTTMessageLib;

public class MQTTNodeServiceNotRegist : MQTTNodeServiceHeader
{
	public MQTTNodeServiceNotRegist(string nodeName, string serviceType)
		: base(nodeName, serviceType, "NotRegist")
	{
		base.EventName = "NotRegist";
	}
}
