using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

public class STNodeHubMulti : STNodeHub
{
	public STNodeHubMulti()
		: base(bSingle: false)
	{
		base.Title = "M_HUB";
	}
}
