using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ST.Library.UI.NodeEditor;

internal class FrmNodePreviewPanel : Form
{
	private bool m_bRight;

	private Point m_ptHandle;

	private int m_nHandleSize;

	private Rectangle m_rect_handle;

	private Rectangle m_rect_panel;

	private Rectangle m_rect_exclude;

	private Region m_region;

	private Type m_type;

	private STNode m_node;

	private STNodeEditor m_editor;

	private STNodePropertyGrid m_property;

	private Pen m_pen = new Pen(Color.Black);

	private SolidBrush m_brush = new SolidBrush(Color.Black);

	private static FrmNodePreviewPanel m_last_frm;

	public Color BorderColor { get; set; }

	public bool AutoBorderColor { get; set; }

	[DllImport("user32.dll")]
	private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

	public FrmNodePreviewPanel(Type stNodeType, Point ptHandle, int nHandleSize, bool bRight, STNodeEditor editor, STNodePropertyGrid propertyGrid)
	{
		SetStyle(ControlStyles.UserPaint, value: true);
		SetStyle(ControlStyles.ResizeRedraw, value: true);
		SetStyle(ControlStyles.AllPaintingInWmPaint, value: true);
		SetStyle(ControlStyles.OptimizedDoubleBuffer, value: true);
		SetStyle(ControlStyles.SupportsTransparentBackColor, value: true);
		if (m_last_frm != null)
		{
			m_last_frm.Close();
		}
		m_last_frm = this;
		m_editor = editor;
		m_property = propertyGrid;
		m_editor.Size = new Size(200, 200);
		m_property.Size = new Size(200, 200);
		m_editor.Location = new Point(1 + (bRight ? nHandleSize : 0), 1);
		m_property.Location = new Point(m_editor.Right, 1);
		m_property.InfoFirstOnDraw = true;
		base.Controls.Add(m_editor);
		base.Controls.Add(m_property);
		base.ShowInTaskbar = false;
		base.FormBorderStyle = FormBorderStyle.None;
		base.Size = new Size(402 + nHandleSize, 202);
		m_type = stNodeType;
		m_ptHandle = ptHandle;
		m_nHandleSize = nHandleSize;
		m_bRight = bRight;
		AutoBorderColor = true;
		BorderColor = Color.DodgerBlue;
	}

	protected override void OnLoad(EventArgs e)
	{
		base.OnLoad(e);
		m_node = (STNode)Activator.CreateInstance(m_type);
		m_node.Left = 20;
		m_node.Top = 20;
		m_editor.Nodes.Add(m_node);
		m_property.SetNode(m_node);
		m_rect_panel = new Rectangle(0, 0, 402, 202);
		m_rect_handle = new Rectangle(m_ptHandle.X, m_ptHandle.Y, m_nHandleSize, m_nHandleSize);
		m_rect_exclude = new Rectangle(0, m_nHandleSize, m_nHandleSize, base.Height - m_nHandleSize);
		if (m_bRight)
		{
			base.Left = m_ptHandle.X;
			m_rect_panel.X = m_ptHandle.X + m_nHandleSize;
		}
		else
		{
			base.Left = m_ptHandle.X - base.Width + m_nHandleSize;
			m_rect_exclude.X = base.Width - m_nHandleSize;
			m_rect_panel.X = base.Left;
		}
		if (m_ptHandle.Y + base.Height > Screen.GetWorkingArea(this).Bottom)
		{
			base.Top = m_ptHandle.Y - base.Height + m_nHandleSize;
			m_rect_exclude.Y -= m_nHandleSize;
		}
		else
		{
			base.Top = m_ptHandle.Y;
		}
		m_rect_panel.Y = base.Top;
		m_region = new Region(new Rectangle(Point.Empty, base.Size));
		m_region.Exclude(m_rect_exclude);
		using (Graphics g = CreateGraphics())
		{
			IntPtr hrgn = m_region.GetHrgn(g);
			SetWindowRgn(base.Handle, hrgn, bRedraw: false);
			m_region.ReleaseHrgn(hrgn);
		}
		base.MouseLeave += Event_MouseLeave;
		m_editor.MouseLeave += Event_MouseLeave;
		m_property.MouseLeave += Event_MouseLeave;
		BeginInvoke((MethodInvoker)delegate
		{
			m_property.Focus();
		});
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		base.OnClosing(e);
		base.Controls.Clear();
		m_editor.Nodes.Clear();
		m_editor.MouseLeave -= Event_MouseLeave;
		m_property.MouseLeave -= Event_MouseLeave;
		m_last_frm = null;
	}

	private void Event_MouseLeave(object sender, EventArgs e)
	{
		Point mousePosition = Control.MousePosition;
		if (!m_rect_panel.Contains(mousePosition) && !m_rect_handle.Contains(mousePosition))
		{
			Close();
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		Graphics graphics = e.Graphics;
		m_pen.Color = (AutoBorderColor ? m_node.TitleColor : BorderColor);
		m_brush.Color = m_pen.Color;
		graphics.DrawRectangle(m_pen, 0, 0, base.Width - 1, base.Height - 1);
		graphics.FillRectangle(m_brush, m_rect_exclude.X - 1, m_rect_exclude.Y - 1, m_rect_exclude.Width + 2, m_rect_exclude.Height + 2);
		Rectangle rectangle = RectangleToClient(m_rect_handle);
		rectangle.Y = (m_nHandleSize - 14) / 2;
		rectangle.X += rectangle.Y + 1;
		int num = (rectangle.Height = 14);
		rectangle.Width = num;
		m_pen.Width = 2f;
		graphics.DrawLine(m_pen, rectangle.X + 4, rectangle.Y + 3, rectangle.X + 10, rectangle.Y + 3);
		graphics.DrawLine(m_pen, rectangle.X + 4, rectangle.Y + 6, rectangle.X + 10, rectangle.Y + 6);
		graphics.DrawLine(m_pen, rectangle.X + 4, rectangle.Y + 11, rectangle.X + 10, rectangle.Y + 11);
		graphics.DrawLine(m_pen, rectangle.X + 7, rectangle.Y + 7, rectangle.X + 7, rectangle.Y + 10);
		m_pen.Width = 1f;
		graphics.DrawRectangle(m_pen, rectangle.X, rectangle.Y, rectangle.Width - 1, rectangle.Height - 1);
	}
}
