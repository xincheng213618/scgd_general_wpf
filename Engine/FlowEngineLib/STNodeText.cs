using System;
using System.Drawing;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

public class STNodeText : STNodeControl
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
		graphics.FillRectangle(Brushes.Gray, base.ClientRectangle);
		m_sf.Alignment = StringAlignment.Near;
		graphics.DrawString(base.Text, base.Font, Brushes.White, base.ClientRectangle, m_sf);
	}
}
