using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace ST.Library.UI.NodeEditor;

internal class FrmSTNodePropertySelect : Form
{
	private STNodePropertyDescriptor m_descriptor;

	private int m_nItemHeight = 25;

	private static Type m_t_bool = typeof(bool);

	private Pen m_pen;

	private SolidBrush m_brush;

	private StringFormat m_sf;

	private Color m_clr_item_1 = Color.FromArgb(10, 0, 0, 0);

	private Color m_clr_item_2 = Color.FromArgb(10, 255, 255, 255);

	private object m_item_hover;

	private List<object> m_lst_item = new List<object>();

	public FrmSTNodePropertySelect(STNodePropertyDescriptor descriptor)
	{
		SetStyle(ControlStyles.UserPaint, value: true);
		SetStyle(ControlStyles.ResizeRedraw, value: true);
		SetStyle(ControlStyles.AllPaintingInWmPaint, value: true);
		SetStyle(ControlStyles.OptimizedDoubleBuffer, value: true);
		SetStyle(ControlStyles.SupportsTransparentBackColor, value: true);
		m_descriptor = descriptor;
		base.Size = descriptor.RectangleR.Size;
		base.ShowInTaskbar = false;
		BackColor = descriptor.Control.BackColor;
		base.FormBorderStyle = FormBorderStyle.None;
		m_pen = new Pen(descriptor.Control.AutoColor ? descriptor.Node.TitleColor : descriptor.Control.ItemSelectedColor, 1f);
		m_brush = new SolidBrush(BackColor);
		m_sf = new StringFormat();
		m_sf.LineAlignment = StringAlignment.Center;
		m_sf.FormatFlags = StringFormatFlags.NoWrap;
	}

	protected override void OnLoad(EventArgs e)
	{
		base.OnLoad(e);
		Point location = m_descriptor.Control.PointToScreen(m_descriptor.RectangleR.Location);
		location.Y += m_descriptor.Control.ScrollOffset;
		base.Location = location;
		if (m_descriptor.PropertyInfo.PropertyType.IsEnum)
		{
			foreach (object value in Enum.GetValues(m_descriptor.PropertyInfo.PropertyType))
			{
				m_lst_item.Add(value);
			}
		}
		else
		{
			if (!(m_descriptor.PropertyInfo.PropertyType == m_t_bool))
			{
				Close();
				return;
			}
			m_lst_item.Add(true);
			m_lst_item.Add(false);
		}
		base.Height = m_lst_item.Count * m_nItemHeight;
		Rectangle workingArea = Screen.GetWorkingArea(this);
		if (base.Bottom > workingArea.Bottom)
		{
			base.Top -= base.Bottom - workingArea.Bottom;
		}
		base.MouseLeave += delegate
		{
			Close();
		};
		base.LostFocus += delegate
		{
			Close();
		};
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		Graphics graphics = e.Graphics;
		graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
		Rectangle rect = new Rectangle(0, 0, base.Width, m_nItemHeight);
		Rectangle rectangle = new Rectangle(10, 0, base.Width - 13, m_nItemHeight);
		int num = 0;
		string stringFromValue = m_descriptor.GetStringFromValue();
		foreach (object item in m_lst_item)
		{
			m_brush.Color = ((num++ % 2 == 0) ? m_clr_item_1 : m_clr_item_2);
			graphics.FillRectangle(m_brush, rect);
			if (item == m_item_hover)
			{
				m_brush.Color = m_descriptor.Control.ItemHoverColor;
				graphics.FillRectangle(m_brush, rect);
			}
			if (item.ToString() == stringFromValue)
			{
				m_brush.Color = m_descriptor.Control.ItemSelectedColor;
				graphics.FillRectangle(m_brush, 4, rect.Top + 10, 5, 5);
			}
			m_brush.Color = m_descriptor.Control.ForeColor;
			graphics.DrawString(item.ToString(), m_descriptor.Control.Font, m_brush, rectangle, m_sf);
			rect.Y += m_nItemHeight;
			rectangle.Y += m_nItemHeight;
		}
		graphics.DrawRectangle(m_pen, 0, 0, base.Width - 1, base.Height - 1);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		int num = e.Y / m_nItemHeight;
		if (num >= 0 && num < m_lst_item.Count)
		{
			object obj = m_lst_item[e.Y / m_nItemHeight];
			if (m_item_hover != obj)
			{
				m_item_hover = obj;
				Invalidate();
			}
		}
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		base.OnMouseClick(e);
		Close();
		int num = e.Y / m_nItemHeight;
		if (num < 0 || num > m_lst_item.Count)
		{
			return;
		}
		try
		{
			m_descriptor.SetValue(m_lst_item[num], null);
		}
		catch (Exception ex)
		{
			m_descriptor.OnSetValueError(ex);
		}
	}
}
