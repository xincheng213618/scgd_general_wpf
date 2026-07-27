using System;
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
			OnPropertyChanged();
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

	protected override void OnGuidRegenerated(Guid oldGuid, Guid newGuid)
	{
		base.OnGuidRegenerated(oldGuid, newGuid);
		NodeID = newGuid.ToString();
	}
}
