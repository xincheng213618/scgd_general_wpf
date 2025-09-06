using System;
using System.Drawing;
using System.Windows.Forms;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

public class STNodeSelectEnumBox : STNodeControl
{
	private Enum _Enum;

	public Enum Enum
	{
		get
		{
			return _Enum;
		}
		set
		{
			_Enum = value;
			Invalidate();
		}
	}

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
		Graphics graphics = dt.Graphics;
		dt.SolidBrush.Color = Color.FromArgb(80, 0, 0, 0);
		graphics.FillRectangle(dt.SolidBrush, base.ClientRectangle);
		m_sf.Alignment = StringAlignment.Near;
		graphics.DrawString(Enum.ToString(), base.Font, Brushes.White, base.ClientRectangle, m_sf);
		graphics.FillPolygon(Brushes.Gray, new Point[3]
		{
			new Point(base.Right - 25, 7),
			new Point(base.Right - 15, 7),
			new Point(base.Right - 20, 12)
		});
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		base.OnMouseClick(e);
		Point pt = new Point(base.Left + base.Owner.Left, base.Top + base.Owner.Top + base.Owner.TitleHeight);
		pt = base.Owner.Owner.CanvasToControl(pt);
		pt = base.Owner.Owner.PointToScreen(pt);
		FrmEnumSelect frmEnumSelect = new FrmEnumSelect(Enum, pt, base.Width, base.Owner.Owner.CanvasScale);
		if (frmEnumSelect.ShowDialog() == DialogResult.OK)
		{
			Enum = frmEnumSelect.Enum;
			OnValueChanged(new EventArgs());
		}
	}
}
