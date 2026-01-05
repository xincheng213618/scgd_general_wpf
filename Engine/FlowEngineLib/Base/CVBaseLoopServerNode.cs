using System.Collections.Generic;

namespace FlowEngineLib.Base;

public abstract class CVBaseLoopServerNode<T> : CVBaseServerNode
{
	protected List<T> _params;

	protected STNodeEditText<string> m_ctrl_Text;

	protected int idx;

	protected CVBaseLoopServerNode(string title, string nodeType, string nodeName, string deviceCode)
		: base(title, nodeType, nodeName, deviceCode)
	{
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		idx = 0;
		m_ctrl_Text = new STNodeEditText<string>();
		m_ctrl_Text.Text = "总步骤 ";
		m_ctrl_Text.DisplayRectangle = m_custom_item;
		if (_params != null)
		{
			m_ctrl_Text.Value = "0/" + _params.Count;
		}
		else
		{
			m_ctrl_Text.Value = "0";
		}
		base.Controls.Add(m_ctrl_Text);
	}

	protected void NextIdx()
	{
		idx++;
		idx %= _params.Count;
	}

	protected override void Reset(CVTransAction trans)
	{
		idx = 0;
		base.ZIndex = -1;
		updateUI();
	}

	protected void updateUI()
	{
		if (_params != null)
		{
			m_ctrl_Text.Value = idx + "/" + _params.Count;
		}
		else
		{
			m_ctrl_Text.Value = "0";
		}
	}

	protected override CVBaseEventObj getBaseEvent(CVStartCFC start)
	{
		CVBaseEventObj cVBaseEventObj = null;
		if (_params != null && _params.Count > 0)
		{
			base.ZIndex = idx + 1;
			cVBaseEventObj = BuildCmd(start, _params[idx]);
			if (cVBaseEventObj != null)
			{
				NextIdx();
				updateUI();
			}
		}
		return cVBaseEventObj;
	}

	protected virtual CVBaseEventObj BuildCmd(CVStartCFC start, T property)
	{
		return new CVBaseEventObj(operatorCode, getBaseEventData(start, property));
	}

	protected abstract object getBaseEventData(CVStartCFC start, T property);
}
