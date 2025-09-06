using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.MQTT;

internal class MQTTObject
{
	public MQActionEvent _MQTTEvent;

	public STNodeOption op;

	public MQTTObject(MQActionEvent e, STNodeOption op)
	{
		_MQTTEvent = e;
		this.op = op;
	}
}
