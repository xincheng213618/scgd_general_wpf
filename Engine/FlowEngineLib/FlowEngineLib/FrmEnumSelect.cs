using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace FlowEngineLib;

public class FrmEnumSelect : Form
{
	private Point m_pt;

	private int m_nWidth;

	private float m_scale;

	private List<object> m_lst = new List<object>();

	private StringFormat m_sf;

	private bool m_bClosed;

	public Enum Enum { get; set; }

	public FrmEnumSelect(Enum e, Point pt, int nWidth, float scale)
	{
		SetStyle(ControlStyles.AllPaintingInWmPaint, value: true);
		SetStyle(ControlStyles.OptimizedDoubleBuffer, value: true);
		foreach (object value in Enum.GetValues(e.GetType()))
		{
			m_lst.Add(value);
		}
		Enum = e;
		m_pt = pt;
		m_scale = scale;
		m_nWidth = nWidth;
		m_sf = new StringFormat();
		m_sf.LineAlignment = StringAlignment.Center;
		base.ShowInTaskbar = false;
		BackColor = Color.FromArgb(255, 34, 34, 34);
		base.FormBorderStyle = FormBorderStyle.None;
	}

	protected override void OnLoad(EventArgs e)
	{
		base.OnLoad(e);
		base.Location = m_pt;
		base.Width = (int)((float)m_nWidth * m_scale);
		base.Height = (int)((float)(m_lst.Count * 20) * m_scale);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		Graphics graphics = e.Graphics;
		graphics.ScaleTransform(m_scale, m_scale);
		Rectangle rectangle = new Rectangle(0, 0, base.Width, 20);
		foreach (object item in m_lst)
		{
			graphics.DrawString(item.ToString(), Font, Brushes.White, rectangle, m_sf);
			rectangle.Y += rectangle.Height;
		}
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		base.OnMouseClick(e);
		int num = e.Y / (int)(20f * m_scale);
		if (num >= 0 && num < m_lst.Count)
		{
			Enum = (Enum)m_lst[num];
		}
		base.DialogResult = DialogResult.OK;
		m_bClosed = true;
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		base.OnMouseLeave(e);
		if (!m_bClosed)
		{
			Close();
		}
	}
}
