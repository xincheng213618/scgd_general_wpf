using System.Drawing;
using System.Windows.Forms;
using FlowEngineLib.Control;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Camera;

internal class ChannelDescriptor : STNodePropertyDescriptor
{
	private Rectangle m_rect;

	protected override void OnSetItemLocation()
	{
		base.OnSetItemLocation();
		Rectangle rectangleR = base.RectangleR;
		m_rect = new Rectangle(rectangleR.Right - 25, rectangleR.Top + 5, 19, 12);
	}

	protected override object GetValueFromString(string strText)
	{
		return Channel.From(strText);
	}

	protected override string GetStringFromValue()
	{
		Channel channel = (Channel)GetValue(null);
		if (channel != null)
		{
			return channel.ToJsonString();
		}
		return "[]";
	}

	protected override void OnDrawValueRectangle(DrawingTools dt)
	{
		base.OnDrawValueRectangle(dt);
		dt.SolidBrush.Color = Color.Gray;
		dt.Graphics.FillRectangle(dt.SolidBrush, m_rect);
		dt.Graphics.DrawRectangle(Pens.Black, m_rect);
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		if (m_rect.Contains(e.Location))
		{
			FormChannel formChannel = new FormChannel();
			formChannel.ChannnelJson = GetStringFromValue();
			if (formChannel.ShowDialog() == DialogResult.OK)
			{
				SetValue(formChannel.ChannnelJson);
			}
		}
		else
		{
			base.OnMouseClick(e);
		}
	}
}
