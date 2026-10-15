using System;
using System.Drawing;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

public class STNodeDevText : STNodeControl
{
	public event EventHandler ValueChanged;

	protected virtual void OnValueChanged(EventArgs e)
	{
		if (this.ValueChanged != null)
		{
			this.ValueChanged(this, e);
		}
	}

	protected override void OnPaint(DrawingTools dt)
	{
		base.OnPaint(dt);
		Graphics graphics = dt.Graphics;
		dt.SolidBrush.Color = Owner?.Owner?.VisualTheme?.Surface ?? Color.DarkBlue;
		graphics.FillRectangle(dt.SolidBrush, base.ClientRectangle);
		dt.SolidBrush.Color = ForeColor;
		m_sf.Alignment = StringAlignment.Center;
		graphics.DrawString(base.Text, base.Font, dt.SolidBrush, base.ClientRectangle, m_sf);
	}
}
