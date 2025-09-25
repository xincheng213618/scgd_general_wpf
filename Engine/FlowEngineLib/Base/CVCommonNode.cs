using System;
using System.Drawing;
using FlowEngineLib.MQTT;
using ST.Library.UI;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Base;

public class CVCommonNode : STNode
{
	protected string m_nodeName;

	protected string m_nodeType;

	protected string m_deviceCode;

	protected int m_zIndex;

	protected int OptionItemHeight;

	[STNodeProperty("服务名称", "服务名称", false, false)]
	public string NodeName
	{
		get
		{
			return m_nodeName;
		}
		set
		{
			string nodeName = m_nodeName;
			m_nodeName = value;
			OnNodeNameChanged(nodeName, value);
		}
	}

	[STNodeProperty("节点类型", "节点类型/类别", false, true, true)]
	public string NodeType
	{
		get
		{
			return m_nodeType;
		}
		set
		{
			m_nodeType = value;
		}
	}

	[STNodeProperty("设备代码", "设备代码", false, false)]
	public string DeviceCode
	{
		get
		{
			return m_deviceCode;
		}
		set
		{
			m_deviceCode = value;
		}
	}

	[STNodeProperty("节点ID", "节点ID", false, false, true)]
	public string NodeID
	{
		get
		{
			return base.Guid.ToString();
		}
		set
		{
		}
	}

	[STNodeProperty("z-index", "z-index", true, false, false)]
	public int ZIndex
	{
		get
		{
			return m_zIndex;
		}
		set
		{
			m_zIndex = value;
		}
	}

	public FlowEngineNodeEvent nodeEvent { get; set; }

	public FlowEngineNodeRunEvent nodeRunEvent { get; set; }

	public FlowEngineNodeEndEvent nodeEndEvent { get; set; }

	protected string NodeKey => $"{NodeID}:{m_zIndex}";

	protected virtual void OnNodeNameChanged(string oldValue, string newValue)
	{
	}

	public CVCommonNode(string title, string nodeType, string nodeName, string deviceCode)
	{
		base.Title = Lang.Get(title);
		m_nodeType = nodeType;
		m_nodeName = nodeName;
		DeviceCode = deviceCode;
		m_zIndex = -1;
		OptionItemHeight = 18;
		base.Height = 90;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
	}

	protected STNodeEditText<T> CreateControl<T>(Type clsType, Rectangle rect, string text, T value)
	{
		STNodeEditText<T> sTNodeEditText = (STNodeEditText<T>)Activator.CreateInstance(clsType);
		sTNodeEditText.Text = text;
		sTNodeEditText.DisplayRectangle = rect;
		sTNodeEditText.Value = value;
		base.Controls.Add(sTNodeEditText);
		return sTNodeEditText;
	}

	protected STNodeDevText CreateTextControl(Rectangle rect, string value)
	{
		STNodeDevText sTNodeDevText = new STNodeDevText();
		sTNodeDevText.Text = value;
		sTNodeDevText.DisplayRectangle = rect;
		base.Controls.Add(sTNodeDevText);
		return sTNodeDevText;
	}

	protected override void OnOwnerChanged()
	{
		base.OnOwnerChanged();
		if (base.Owner != null)
		{
			base.Owner.SetTypeColor(typeof(string), Color.Yellow);
			base.Owner.SetTypeColor(typeof(bool), Color.DodgerBlue, bReplace: true);
			base.Owner.SetTypeColor(typeof(CVLoopCFC), Color.DodgerBlue, bReplace: true);
			base.Owner.SetTypeColor(typeof(int), Color.Cornsilk, bReplace: true);
			base.Owner.SetTypeColor(typeof(MQActionEvent), Color.DeepPink, bReplace: true);
			base.Owner.SetTypeColor(typeof(CVMQTTRequest), Color.DeepPink, bReplace: true);
			base.Owner.SetTypeColor(typeof(CVBaseDataFlowResp), Color.DeepPink, bReplace: true);
			base.Owner.SetTypeColor(typeof(CVStartCFC), Color.DarkGreen, bReplace: true);
		}
	}

	protected bool HasData(STNodeOptionEventArgs e)
	{
		if (e.Status == ConnectionStatus.Connected)
		{
			return e.TargetOption.Data != null;
		}
		return false;
	}

	public string ToShortString()
	{
		return $"{base.Title}/{DeviceCode}/{NodeID}/{ZIndex}";
	}
}
