using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

public class STNodeHubSingle : STNodeHub
{
	public STNodeHubSingle()
		: base(bSingle: true)
	{
		base.Title = "S_HUB";
	}
}
