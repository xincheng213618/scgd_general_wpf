using System;
using System.Drawing;
using System.Windows.Forms;
using ST.Library.UI.NodeEditor;

namespace ST.Library.UI;

internal class FrmSTNodePropertyInput : Form
{
	private STNodePropertyDescriptor m_descriptor;

	private Rectangle m_rect;

	private Pen m_pen;

	private SolidBrush m_brush;

	private TextBox m_tbx;

	public FrmSTNodePropertyInput(STNodePropertyDescriptor descriptor)
	{
		SetStyle(ControlStyles.UserPaint, value: true);
		SetStyle(ControlStyles.ResizeRedraw, value: true);
		SetStyle(ControlStyles.AllPaintingInWmPaint, value: true);
		SetStyle(ControlStyles.OptimizedDoubleBuffer, value: true);
		SetStyle(ControlStyles.SupportsTransparentBackColor, value: true);
		m_rect = descriptor.RectangleR;
		m_descriptor = descriptor;
		base.ShowInTaskbar = false;
		base.FormBorderStyle = FormBorderStyle.None;
		BackColor = (descriptor.Control.AutoColor ? descriptor.Node.TitleColor : descriptor.Control.ItemSelectedColor);
		m_pen = new Pen(descriptor.Control.ForeColor, 1f);
		m_brush = new SolidBrush(BackColor);
	}

	protected override void OnLoad(EventArgs e)
	{
		base.OnLoad(e);
		Point location = m_descriptor.Control.PointToScreen(m_rect.Location);
		location.Y += m_descriptor.Control.ScrollOffset;
		base.Location = location;
		base.Size = new Size(m_rect.Width + m_rect.Height, m_rect.Height);
		m_tbx = new TextBox();
		m_tbx.Font = m_descriptor.Control.Font;
		m_tbx.ForeColor = m_descriptor.Control.ForeColor;
		m_tbx.BackColor = Color.FromArgb(255, m_descriptor.Control.ItemValueBackColor);
		m_tbx.BorderStyle = BorderStyle.None;
		m_tbx.Size = new Size(base.Width - 4 - m_rect.Height, base.Height - 2);
		m_tbx.Text = m_descriptor.GetStringFromValue();
		base.Controls.Add(m_tbx);
		m_tbx.Location = new Point(2, (base.Height - m_tbx.Height) / 2);
		m_tbx.SelectAll();
		m_tbx.LostFocus += delegate
		{
			Close();
		};
		m_tbx.KeyDown += tbx_KeyDown;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		Graphics graphics = e.Graphics;
		m_brush.Color = m_tbx.BackColor;
		graphics.FillRectangle(m_brush, 1, 1, base.Width - 2 - m_rect.Height, base.Height - 2);
		m_brush.Color = m_descriptor.Control.ForeColor;
		graphics.FillPolygon(m_brush, new Point[3]
		{
			new Point(base.Width - 21, base.Height - 2),
			new Point(base.Width - 14, base.Height - 2),
			new Point(base.Width - 14, base.Height - 8)
		});
		graphics.DrawLine(m_pen, base.Width - 14, base.Height - 3, base.Width - 4, base.Height - 3);
		graphics.DrawLine(m_pen, base.Width - 4, base.Height - 3, base.Width - 4, 14);
		graphics.DrawLine(m_pen, base.Width - 8, 13, base.Width - 4, 13);
		graphics.DrawLine(m_pen, base.Width - 19, 11, base.Width - 4, 11);
		graphics.DrawLine(m_pen, base.Width - 19, 3, base.Width - 16, 3);
		graphics.DrawLine(m_pen, base.Width - 19, 6, base.Width - 16, 6);
		graphics.DrawLine(m_pen, base.Width - 19, 9, base.Width - 16, 9);
		graphics.DrawLine(m_pen, base.Width - 19, 3, base.Width - 19, 9);
		graphics.DrawLine(m_pen, base.Width - 13, 3, base.Width - 10, 3);
		graphics.DrawLine(m_pen, base.Width - 13, 6, base.Width - 10, 6);
		graphics.DrawLine(m_pen, base.Width - 13, 9, base.Width - 10, 9);
		graphics.DrawLine(m_pen, base.Width - 13, 3, base.Width - 13, 6);
		graphics.DrawLine(m_pen, base.Width - 10, 6, base.Width - 10, 9);
		graphics.DrawLine(m_pen, base.Width - 7, 3, base.Width - 4, 3);
		graphics.DrawLine(m_pen, base.Width - 7, 9, base.Width - 4, 9);
		graphics.DrawLine(m_pen, base.Width - 7, 3, base.Width - 7, 9);
	}

	private void tbx_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.KeyCode == Keys.Escape)
		{
			Close();
		}
		if (e.KeyCode == Keys.Return)
		{
			try
			{
				m_descriptor.SetValue(((TextBox)sender).Text, null);
				m_descriptor.Control.Invalidate();
			}
			catch (Exception ex)
			{
				m_descriptor.OnSetValueError(ex);
			}
			Close();
		}
	}

	private void InitializeComponent()
	{
		base.SuspendLayout();
		base.ClientSize = new System.Drawing.Size(292, 273);
		base.Name = "FrmSTNodePropertyInput";
		base.ResumeLayout(false);
	}
}
