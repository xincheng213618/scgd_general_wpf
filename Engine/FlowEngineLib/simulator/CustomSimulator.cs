using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.simulator;

internal class CustomSimulator : BaseSimulator
{
	[STNodeProperty("Topic", "Topic")]
	public string Topic
	{
		get
		{
			return nodeCode;
		}
		set
		{
			nodeCode = value;
		}
	}

	public CustomSimulator()
		: base("自定义模拟器", "CUSTOM", "DEV01")
	{
	}
}
