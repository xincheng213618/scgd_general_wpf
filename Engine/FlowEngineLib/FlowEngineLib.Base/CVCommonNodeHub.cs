using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Base;

public class CVCommonNodeHub : STNodeHub
{
	private string m_nodeId;

	[STNodeProperty("节点ID", "节点ID", false, false, true)]
	public string NodeID
	{
		get
		{
			return m_nodeId;
		}
		set
		{
			m_nodeId = value;
		}
	}

	public CVCommonNodeHub()
		: this(bSingle: false, string.Empty)
	{
	}

	public CVCommonNodeHub(bool bSingle, string title)
		: base(bSingle, title)
	{
		m_nodeId = base.Guid.ToString();
	}
}
