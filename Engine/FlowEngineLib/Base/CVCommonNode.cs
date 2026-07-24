#pragma warning disable CA1507,CA1866
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using FlowEngineLib.MQTT;
using ST.Library.UI;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Base;

public class CVCommonNode : STNode
{
	public const int StandardNodeWidth = 160;
	public const int StandardNodeMinHeight = 80;
	protected const int StandardNodeContentPadding = 5;
	protected const int StandardNodeContentWidth = StandardNodeWidth - StandardNodeContentPadding * 2;
	protected const int CompactSummaryTop = 30;
	protected const int CompactSummaryHeight = 18;

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
            OnPropertyChanged();
        }
    }

	public string NodeType
	{
		get
		{
			return m_nodeType;
		}
		set
		{
			m_nodeType = value;
            OnPropertyChanged();
        }
    }

	[STNodeProperty("设备代码", "设备代码", false, true)]
	public string DeviceCode
	{
		get
		{
			return m_deviceCode;
		}
		set
		{
			m_deviceCode = value;
			OnPropertyChanged();
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
            OnPropertyChanged();
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
            OnPropertyChanged();
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

	protected override void OnCreated()
	{
		base.OnCreated();
		ApplyCompactNodeDisplay();
	}

	protected override void OnEditorLoadCompleted()
	{
		base.OnEditorLoadCompleted();
		ApplyCompactNodeDisplay();
	}

	public virtual void ApplyCompactNodeDisplay()
	{
		ShowControls = false;
		SetAutoSize(true);
	}

	protected override void OnDrawBody(DrawingTools dt)
	{
		base.OnDrawBody(dt);
		if (!ShouldDrawCompactSummary())
		{
			return;
		}

		DrawCompactSummary(dt, GetCompactSummaryLabel(), GetCompactSummaryValue());
	}

	protected virtual string GetCompactSummaryLabel()
	{
		return string.Empty;
	}

	protected virtual string GetCompactSummaryValue()
	{
		return string.Empty;
	}

	protected bool ShouldDrawCompactSummary()
	{
		return !ShowControls && InputOptionsCount < 2 && !string.IsNullOrEmpty(GetCompactSummaryValue());
	}

	private void DrawCompactSummary(DrawingTools dt, string label, string value)
	{
		Rectangle rectangle = new Rectangle(
			Left + StandardNodeContentPadding,
			Top + TitleHeight + CompactSummaryTop,
			Math.Max(0, Width - StandardNodeContentPadding * 2),
			CompactSummaryHeight);
		Graphics graphics = dt.Graphics;
		GraphicsState state = graphics.Save();
		StringAlignment alignment = m_sf.Alignment;
		StringAlignment lineAlignment = m_sf.LineAlignment;
		StringFormatFlags formatFlags = m_sf.FormatFlags;
		StringTrimming trimming = m_sf.Trimming;
		try
		{
			graphics.SetClip(rectangle, CombineMode.Intersect);
			graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
			dt.SolidBrush.Color = ForeColor;
			m_sf.LineAlignment = StringAlignment.Center;
			m_sf.FormatFlags |= StringFormatFlags.NoWrap;
			m_sf.Trimming = StringTrimming.None;
			if (!string.IsNullOrEmpty(label))
			{
				m_sf.Alignment = StringAlignment.Near;
				graphics.DrawString(label, Font, dt.SolidBrush, rectangle, m_sf);
			}
			m_sf.Alignment = string.IsNullOrEmpty(label) ? StringAlignment.Near : StringAlignment.Far;
			graphics.DrawString(value, Font, dt.SolidBrush, rectangle, m_sf);
		}
		finally
		{
			m_sf.Alignment = alignment;
			m_sf.LineAlignment = lineAlignment;
			m_sf.FormatFlags = formatFlags;
			m_sf.Trimming = trimming;
			graphics.Restore(state);
		}
	}

	protected override Size GetDefaultNodeSize(Graphics g)
	{
		Size size = base.GetDefaultNodeSize(g);
		return new Size(StandardNodeWidth, Math.Max(StandardNodeMinHeight, size.Height));
	}

	protected STNodeEditText<T> CreateControl<T>(Type clsType, Rectangle rect, string text, T value)
	{
		if (clsType == null)
		{
			throw new ArgumentNullException("clsType");
		}
		STNodeEditText<T> sTNodeEditText = (STNodeEditText<T>)Activator.CreateInstance(clsType);
		sTNodeEditText.Text = GetLocalizedText(text);
		sTNodeEditText.DisplayRectangle = rect;
		sTNodeEditText.Value = value;
		base.Controls.Add(sTNodeEditText);
		return sTNodeEditText;
	}

	private string GetLocalizedText(string text)
	{
		string text2 = text;
		if (text.EndsWith(":"))
		{
			return Lang.Get(text.Substring(0, text.Length - 1)) + ":";
		}
		return Lang.Get(text);
	}

	protected STNodeEditText<string> CreateStringControl(Rectangle rect, string text, string value)
	{
		return CreateControl(typeof(STNodeEditText<string>), rect, text, value);
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
		return base.Title;
	}
}
