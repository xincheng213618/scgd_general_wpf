using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.Reflection;
using System.Windows.Forms;

namespace ST.Library.UI.NodeEditor;

public class STNodePropertyGrid : Control
{
	private STNode _STNode;

	private Color _ItemHoverColor = Color.FromArgb(50, 125, 125, 125);

	private Color _ItemSelectedColor = Color.DodgerBlue;

	private Color _ItemValueBackColor = Color.FromArgb(255, 80, 80, 80);

	private Color _TitleColor = Color.FromArgb(127, 0, 0, 0);

	private Color _ErrorColor = Color.FromArgb(200, Color.Brown);

	private Color _DescriptionColor = Color.FromArgb(200, Color.DarkGoldenrod);

	private bool _ShowTitle = true;

	private bool _AutoColor = true;

	private bool _InfoFirstOnDraw;

	private bool _ReadOnlyModel;

	private bool _IsEditEnable = true;

	protected Rectangle m_rect_link;

	protected Rectangle m_rect_help;

	protected Rectangle m_rect_title;

	protected Rectangle m_rect_switch;

	protected int m_nOffsetY;

	protected int m_nInfoOffsetY;

	protected int m_nPropertyOffsetY;

	protected int m_nVHeight;

	protected int m_nInfoVHeight;

	protected int m_nPropertyVHeight;

	protected int m_nInfoLeft;

	private Type m_type;

	private string[] m_KeysString = new string[4] { "作者", "邮箱", "链接", "查看帮助" };

	private int m_nTitleHeight = 20;

	private int m_item_height = 30;

	private Color m_clr_item_1 = Color.FromArgb(10, 0, 0, 0);

	private Color m_clr_item_2 = Color.FromArgb(10, 255, 255, 255);

	private List<STNodePropertyDescriptor> m_lst_item = new List<STNodePropertyDescriptor>();

	private STNodePropertyDescriptor m_item_hover;

	private STNodePropertyDescriptor m_item_hover_value;

	private STNodePropertyDescriptor m_item_down_value;

	private STNodePropertyDescriptor m_item_selected;

	private STNodeAttribute m_node_attribute;

	private bool m_b_hover_switch;

	private bool m_b_current_draw_info;

	private Point m_pt_move;

	private Point m_pt_down;

	private string m_str_err;

	private string m_str_desc;

	private Pen m_pen;

	private SolidBrush m_brush;

	private StringFormat m_sf;

	private DrawingTools m_dt;

	[Description("当前显示的STNode")]
	[Browsable(false)]
	public STNode STNode => _STNode;

	[Description("获取或设置属性选项被鼠标悬停时候背景色")]
	public Color ItemHoverColor
	{
		get
		{
			return _ItemHoverColor;
		}
		set
		{
			_ItemHoverColor = value;
		}
	}

	[Description("获取或设置属性选项被选中时候背景色 当AutoColor被设置时此属性不能被设置")]
	[DefaultValue(typeof(Color), "DodgerBlue")]
	public Color ItemSelectedColor
	{
		get
		{
			return _ItemSelectedColor;
		}
		set
		{
			if (!_AutoColor && !(value == _ItemSelectedColor))
			{
				_ItemSelectedColor = value;
				Invalidate();
			}
		}
	}

	[Description("获取或设置属性选项值背景色")]
	public Color ItemValueBackColor
	{
		get
		{
			return _ItemValueBackColor;
		}
		set
		{
			_ItemValueBackColor = value;
			Invalidate();
		}
	}

	[Description("获取或设置默认标题背景色")]
	public Color TitleColor
	{
		get
		{
			return _TitleColor;
		}
		set
		{
			_TitleColor = value;
			if (_ShowTitle)
			{
				Invalidate(m_rect_title);
			}
		}
	}

	[Description("获取或设置属性设置错误时候提示信息背景色")]
	public Color ErrorColor
	{
		get
		{
			return _ErrorColor;
		}
		set
		{
			_ErrorColor = value;
		}
	}

	[Description("获取或设置属性描述信息背景色")]
	public Color DescriptionColor
	{
		get
		{
			return _DescriptionColor;
		}
		set
		{
			_DescriptionColor = value;
		}
	}

	[Description("获取或设置是否显示节点标题")]
	public bool ShowTitle
	{
		get
		{
			return _ShowTitle;
		}
		set
		{
			_ShowTitle = value;
			SetItemRectangle();
			Invalidate();
		}
	}

	[Description("获取或设置是否根据STNode自动设置控件高亮颜色")]
	[DefaultValue(true)]
	public bool AutoColor
	{
		get
		{
			return _AutoColor;
		}
		set
		{
			_AutoColor = value;
		}
	}

	[Description("获取或设置当节点被设置时候 是否优先绘制信息面板")]
	[DefaultValue(false)]
	public bool InfoFirstOnDraw
	{
		get
		{
			return _InfoFirstOnDraw;
		}
		set
		{
			_InfoFirstOnDraw = value;
		}
	}

	[Description("获取或设置当前属性编辑器是否处于只读模式")]
	[DefaultValue(false)]
	public bool ReadOnlyModel
	{
		get
		{
			return _ReadOnlyModel;
		}
		set
		{
			if (value != _ReadOnlyModel)
			{
				_ReadOnlyModel = value;
				Invalidate(m_rect_title);
			}
		}
	}

	[Description("获取当前滚动条高度")]
	public int ScrollOffset => m_nOffsetY;

	[Description("获取或设置是否可编辑")]
	[DefaultValue(true)]
	public bool IsEditEnable
	{
		get
		{
			return _IsEditEnable;
		}
		set
		{
			_IsEditEnable = value;
		}
	}

	public STNodePropertyGrid()
	{
		SetStyle(ControlStyles.UserPaint, value: true);
		SetStyle(ControlStyles.ResizeRedraw, value: true);
		SetStyle(ControlStyles.AllPaintingInWmPaint, value: true);
		SetStyle(ControlStyles.OptimizedDoubleBuffer, value: true);
		SetStyle(ControlStyles.SupportsTransparentBackColor, value: true);
		m_pen = new Pen(Color.Black, 1f);
		m_brush = new SolidBrush(Color.Black);
		m_sf = new StringFormat();
		m_sf.LineAlignment = StringAlignment.Center;
		m_sf.FormatFlags = StringFormatFlags.NoWrap;
		m_dt.Pen = m_pen;
		m_dt.SolidBrush = m_brush;
		ForeColor = Color.White;
		BackColor = Color.FromArgb(255, 35, 35, 35);
		MinimumSize = new Size(120, 50);
		base.Size = new Size(200, 150);
	}

	private List<STNodePropertyDescriptor> GetProperties(STNode node)
	{
		List<STNodePropertyDescriptor> graphics = new List<STNodePropertyDescriptor>();
		if (node == null)
		{
			return graphics;
		}
		Type num = node.GetType();
		PropertyInfo[] properties = num.GetProperties();
		foreach (PropertyInfo propertyInfo in properties)
		{
			object[] customAttributes = propertyInfo.GetCustomAttributes(inherit: true);
			object[] array = customAttributes;
			foreach (object obj in array)
			{
				if (obj is STNodePropertyAttribute)
				{
					STNodePropertyAttribute sTNodePropertyAttribute = obj as STNodePropertyAttribute;
					object obj2 = Activator.CreateInstance(sTNodePropertyAttribute.DescriptorType);
					if (!(obj2 is STNodePropertyDescriptor))
					{
						throw new ArgumentException("[STNodePropertyAttribute.DescriptorType]参数值必须为[STNodePropertyDescriptor]或者其子类的类型");
					}
					STNodePropertyDescriptor sTNodePropertyDescriptor = (STNodePropertyDescriptor)Activator.CreateInstance(sTNodePropertyAttribute.DescriptorType);
					sTNodePropertyDescriptor.Node = node;
					string name = Lang.Get(sTNodePropertyAttribute.Name);
					sTNodePropertyDescriptor.Name = name;
					sTNodePropertyDescriptor.Description = sTNodePropertyAttribute.Description;
					sTNodePropertyDescriptor.PropertyInfo = propertyInfo;
					sTNodePropertyDescriptor.IsEditEnable = IsEditEnable || sTNodePropertyAttribute.IsEditEnable;
					sTNodePropertyDescriptor.IsReadOnly = sTNodePropertyAttribute.IsReadOnly;
					sTNodePropertyDescriptor.Control = this;
					if (IsEditEnable || !sTNodePropertyAttribute.IsHide)
					{
						graphics.Add(sTNodePropertyDescriptor);
					}
				}
			}
		}
		return graphics;
	}

	private STNodeAttribute GetNodeAttribute(STNode node)
	{
		if (node == null)
		{
			return null;
		}
		Type graphics = node.GetType();
		object[] num = graphics.GetCustomAttributes(inherit: true);
		foreach (object array in num)
		{
			if (array is STNodeAttribute)
			{
				return (STNodeAttribute)array;
			}
		}
		return null;
	}

	private void SetItemRectangle()
	{
		int graphics = 0;
		int num = 0;
		using Graphics rect2 = CreateGraphics();
		foreach (STNodePropertyDescriptor item in m_lst_item)
		{
			SizeF sizeF = rect2.MeasureString(item.Name, Font);
			if (sizeF.Width > (float)graphics)
			{
				graphics = (int)Math.Ceiling(sizeF.Width);
			}
		}
		for (int i = 0; i < m_KeysString.Length - 1; i++)
		{
			SizeF sizeF2 = rect2.MeasureString(m_KeysString[i], Font);
			if (sizeF2.Width > (float)num)
			{
				num = (int)Math.Ceiling(sizeF2.Width);
			}
		}
		graphics += 5;
		num += 5;
		graphics = Math.Min(graphics, base.Width >> 1);
		m_nInfoLeft = Math.Min(num, base.Width >> 1);
		int num2 = (_ShowTitle ? m_nTitleHeight : 0);
		for (int j = 0; j < m_lst_item.Count; j++)
		{
			STNodePropertyDescriptor sTNodePropertyDescriptor = m_lst_item[j];
			Rectangle rectangle = (sTNodePropertyDescriptor.Rectangle = new Rectangle(0, j * m_item_height + num2, base.Width, m_item_height));
			rectangle.Width = graphics;
			sTNodePropertyDescriptor.RectangleL = rectangle;
			rectangle.X = rectangle.Right;
			rectangle.Width = base.Width - rectangle.Left - 1;
			rectangle.Inflate(-4, -4);
			sTNodePropertyDescriptor.RectangleR = rectangle;
			sTNodePropertyDescriptor.OnSetItemLocation();
		}
		m_nPropertyVHeight = m_lst_item.Count * m_item_height;
		if (_ShowTitle)
		{
			m_nPropertyVHeight += m_nTitleHeight;
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		Graphics graphics = e.Graphics;
		graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
		m_dt.Graphics = graphics;
		m_nOffsetY = (m_b_current_draw_info ? m_nInfoOffsetY : m_nPropertyOffsetY);
		graphics.TranslateTransform(0f, m_nOffsetY);
		if (m_b_current_draw_info)
		{
			m_nVHeight = m_nInfoVHeight;
			OnDrawInfo(m_dt);
		}
		else
		{
			m_nVHeight = m_nPropertyVHeight;
			for (int i = 0; i < m_lst_item.Count; i++)
			{
				OnDrawPropertyItem(m_dt, m_lst_item[i], i);
			}
		}
		graphics.ResetTransform();
		if (_ShowTitle)
		{
			OnDrawTitle(m_dt);
		}
		m_sf.FormatFlags = (StringFormatFlags)0;
		if (!string.IsNullOrEmpty(m_str_err))
		{
			OnDrawErrorInfo(m_dt);
		}
		if (!string.IsNullOrEmpty(m_str_desc))
		{
			OnDrawDescription(m_dt);
		}
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		m_pt_move = e.Location;
		bool flag = _ShowTitle && m_rect_switch.Contains(e.Location);
		if (flag != m_b_hover_switch)
		{
			m_b_hover_switch = flag;
			Invalidate(m_rect_switch);
		}
		Point point = new Point(e.X, e.Y - m_nOffsetY);
		MouseEventArgs e2 = new MouseEventArgs(e.Button, e.Clicks, point.X, point.Y, e.Delta);
		if (m_b_current_draw_info)
		{
			OnProcessHelpMouseMove(e2);
		}
		else
		{
			OnProcessPropertyMouseMove(e2);
		}
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		base.OnMouseDown(e);
		m_pt_down = e.Location;
		Focus();
		bool sTNodeTreeCollection = false;
		if (m_str_err != null)
		{
			sTNodeTreeCollection = true;
			m_str_err = null;
		}
		if (_ShowTitle)
		{
			if (m_rect_switch.Contains(e.Location))
			{
				if (m_node_attribute != null && m_lst_item.Count != 0)
				{
					m_b_current_draw_info = !m_b_current_draw_info;
					Invalidate();
				}
				return;
			}
			if (m_rect_title.Contains(e.Location))
			{
				return;
			}
		}
		if (_ShowTitle && m_rect_switch.Contains(e.Location))
		{
			if (m_node_attribute != null && m_lst_item.Count != 0)
			{
				m_b_current_draw_info = !m_b_current_draw_info;
				Invalidate();
			}
			return;
		}
		Point point = new Point(e.X, e.Y - m_nOffsetY);
		MouseEventArgs e2 = new MouseEventArgs(e.Button, e.Clicks, point.X, point.Y, e.Delta);
		if (m_b_current_draw_info)
		{
			OnProcessInfoMouseDown(e2);
		}
		else
		{
			OnProcessPropertyMouseDown(e2);
		}
		if (sTNodeTreeCollection)
		{
			Invalidate();
		}
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		base.OnMouseUp(e);
		m_str_desc = null;
		if (m_item_down_value != null && !_ReadOnlyModel)
		{
			Point point = new Point(e.X, e.Y - m_nOffsetY);
			MouseEventArgs e2 = new MouseEventArgs(e.Button, e.Clicks, point.X, point.Y, e.Delta);
			m_item_down_value.OnMouseUp(e2);
			if (m_pt_down == e.Location && !_ReadOnlyModel)
			{
				m_item_down_value.OnMouseClick(e2);
			}
		}
		m_item_down_value = null;
		Invalidate();
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		base.OnMouseLeave(e);
		m_b_hover_switch = false;
		if (m_item_hover_value != null && !_ReadOnlyModel)
		{
			m_item_hover_value.OnMouseLeave(e);
		}
		m_item_hover = null;
		Invalidate();
	}

	protected override void OnMouseWheel(MouseEventArgs e)
	{
		base.OnMouseWheel(e);
		if (e.Delta > 0)
		{
			if (m_nOffsetY == 0)
			{
				return;
			}
			m_nOffsetY += m_item_height;
			if (m_nOffsetY > 0)
			{
				m_nOffsetY = 0;
			}
		}
		else
		{
			if (base.Height - m_nOffsetY >= m_nVHeight)
			{
				return;
			}
			m_nOffsetY -= m_item_height;
		}
		if (m_b_current_draw_info)
		{
			m_nInfoOffsetY = m_nOffsetY;
		}
		else
		{
			m_nPropertyOffsetY = m_nOffsetY;
		}
		Invalidate();
	}

	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		m_rect_title.Width = base.Width;
		m_rect_title.Height = m_nTitleHeight;
		if (_ShowTitle)
		{
			m_rect_switch = new Rectangle(base.Width - m_nTitleHeight + 2, 2, m_nTitleHeight - 4, m_nTitleHeight - 4);
		}
		if (_STNode != null)
		{
			SetItemRectangle();
		}
	}

	protected virtual void OnDrawPropertyItem(DrawingTools dt, STNodePropertyDescriptor item, int nIndex)
	{
		Graphics graphics = dt.Graphics;
		m_brush.Color = ((nIndex % 2 == 0) ? m_clr_item_1 : m_clr_item_2);
		graphics.FillRectangle(m_brush, item.Rectangle);
		if (item == m_item_hover || item == m_item_selected)
		{
			m_brush.Color = _ItemHoverColor;
			graphics.FillRectangle(m_brush, item.Rectangle);
		}
		if (m_item_selected == item)
		{
			graphics.FillRectangle(m_brush, item.Rectangle.X, item.Rectangle.Y, 5, item.Rectangle.Height);
			if (_AutoColor && _STNode != null)
			{
				m_brush.Color = _STNode.TitleColor;
			}
			else
			{
				m_brush.Color = _ItemSelectedColor;
			}
			graphics.FillRectangle(m_brush, item.Rectangle.X, item.Rectangle.Y + 4, 5, item.Rectangle.Height - 8);
		}
		m_sf.Alignment = StringAlignment.Far;
		m_brush.Color = ForeColor;
		graphics.DrawString(item.Name, Font, m_brush, item.RectangleL, m_sf);
		item.OnDrawValueRectangle(m_dt);
		if (_ReadOnlyModel)
		{
			m_brush.Color = Color.FromArgb(125, 125, 125, 125);
			graphics.FillRectangle(m_brush, item.RectangleR);
			m_pen.Color = ForeColor;
		}
	}

	protected virtual void OnDrawTitle(DrawingTools dt)
	{
		Graphics graphics = dt.Graphics;
		if (_AutoColor)
		{
			m_brush.Color = ((_STNode == null) ? _TitleColor : _STNode.TitleColor);
		}
		else
		{
			m_brush.Color = _TitleColor;
		}
		graphics.FillRectangle(m_brush, m_rect_title);
		m_brush.Color = ((_STNode == null) ? ForeColor : _STNode.ForeColor);
		m_sf.Alignment = StringAlignment.Center;
		graphics.DrawString((_STNode == null) ? Text : _STNode.Title, Font, m_brush, m_rect_title, m_sf);
		if (_ReadOnlyModel)
		{
			m_brush.Color = ForeColor;
			graphics.FillRectangle(dt.SolidBrush, 4, 5, 2, 4);
			graphics.FillRectangle(dt.SolidBrush, 6, 5, 2, 2);
			graphics.FillRectangle(dt.SolidBrush, 8, 5, 2, 4);
			graphics.FillRectangle(dt.SolidBrush, 3, 9, 8, 6);
		}
		if (m_node_attribute != null && m_lst_item.Count != 0)
		{
			if (m_b_hover_switch)
			{
				m_brush.Color = BackColor;
				graphics.FillRectangle(m_brush, m_rect_switch);
			}
			m_pen.Color = ((_STNode == null) ? ForeColor : _STNode.ForeColor);
			m_brush.Color = m_pen.Color;
			int num = m_rect_switch.Top + m_rect_switch.Height / 2 - 2;
			int num2 = m_rect_switch.Top + m_rect_switch.Height / 2 + 1;
			graphics.DrawRectangle(m_pen, m_rect_switch.Left, m_rect_switch.Top, m_rect_switch.Width - 1, m_rect_switch.Height - 1);
			graphics.DrawLines(m_pen, new Point[4]
			{
				new Point(m_rect_switch.Left + 2, num),
				new Point(m_rect_switch.Right - 3, num),
				new Point(m_rect_switch.Left + 3, num - 1),
				new Point(m_rect_switch.Right - 3, num - 1)
			});
			graphics.DrawLines(m_pen, new Point[4]
			{
				new Point(m_rect_switch.Left + 2, num2),
				new Point(m_rect_switch.Right - 3, num2),
				new Point(m_rect_switch.Left + 2, num2 + 1),
				new Point(m_rect_switch.Right - 4, num2 + 1)
			});
			graphics.FillPolygon(m_brush, new Point[3]
			{
				new Point(m_rect_switch.Left + 2, num),
				new Point(m_rect_switch.Left + 7, num),
				new Point(m_rect_switch.Left + 7, m_rect_switch.Top)
			});
			graphics.FillPolygon(m_brush, new Point[3]
			{
				new Point(m_rect_switch.Right - 2, num2),
				new Point(m_rect_switch.Right - 7, num2),
				new Point(m_rect_switch.Right - 7, m_rect_switch.Bottom - 2)
			});
		}
	}

	protected virtual void OnDrawDescription(DrawingTools dt)
	{
		if (!string.IsNullOrEmpty(m_str_desc))
		{
			Graphics graphics = dt.Graphics;
			SizeF sizeF = graphics.MeasureString(m_str_desc, Font, base.Width - 4);
			Rectangle rectangle = new Rectangle(0, base.Height - (int)sizeF.Height - 4, base.Width, (int)sizeF.Height + 4);
			m_brush.Color = _DescriptionColor;
			graphics.FillRectangle(m_brush, rectangle);
			m_pen.Color = _DescriptionColor;
			graphics.DrawRectangle(m_pen, 0, rectangle.Top, rectangle.Width - 1, rectangle.Height - 1);
			rectangle.Inflate(-4, 0);
			m_brush.Color = ForeColor;
			m_sf.Alignment = StringAlignment.Near;
			graphics.DrawString(m_str_desc, Font, m_brush, rectangle, m_sf);
		}
	}

	protected virtual void OnDrawErrorInfo(DrawingTools dt)
	{
		if (!string.IsNullOrEmpty(m_str_err))
		{
			Graphics graphics = dt.Graphics;
			SizeF sizeF = graphics.MeasureString(m_str_err, Font, base.Width - 4);
			Rectangle rectangle = new Rectangle(0, 0, base.Width, (int)sizeF.Height + 4);
			m_brush.Color = _ErrorColor;
			graphics.FillRectangle(m_brush, rectangle);
			m_pen.Color = _ErrorColor;
			graphics.DrawRectangle(m_pen, 0, rectangle.Top, rectangle.Width - 1, rectangle.Height - 1);
			rectangle.Inflate(-4, 0);
			m_brush.Color = ForeColor;
			m_sf.Alignment = StringAlignment.Near;
			graphics.DrawString(m_str_err, Font, m_brush, rectangle, m_sf);
		}
	}

	protected virtual void OnDrawInfo(DrawingTools dt)
	{
		if (m_node_attribute != null)
		{
			STNodeAttribute node_attribute = m_node_attribute;
			Graphics graphics = dt.Graphics;
			Color color = Color.FromArgb(ForeColor.A / 2, ForeColor);
			m_sf.Alignment = StringAlignment.Near;
			Rectangle rectangle = new Rectangle(0, _ShowTitle ? m_nTitleHeight : 0, base.Width, m_item_height);
			Rectangle rectangle2 = new Rectangle(2, rectangle.Top, m_nInfoLeft - 2, m_item_height);
			Rectangle rectangle3 = new Rectangle(m_nInfoLeft, rectangle.Top, base.Width - m_nInfoLeft, m_item_height);
			m_brush.Color = m_clr_item_2;
			graphics.FillRectangle(m_brush, rectangle);
			m_brush.Color = ForeColor;
			m_sf.FormatFlags = StringFormatFlags.NoWrap;
			m_sf.Alignment = StringAlignment.Near;
			graphics.DrawString(m_KeysString[0], Font, m_brush, rectangle2, m_sf);
			m_brush.Color = color;
			graphics.DrawString(node_attribute.Author, Font, m_brush, rectangle3, m_sf);
			rectangle.Y += m_item_height;
			rectangle2.Y += m_item_height;
			rectangle3.Y += m_item_height;
			m_brush.Color = m_clr_item_1;
			graphics.FillRectangle(m_brush, rectangle);
			m_brush.Color = ForeColor;
			graphics.DrawString(m_KeysString[1], Font, m_brush, rectangle2, m_sf);
			m_brush.Color = color;
			graphics.DrawString(node_attribute.Mail, Font, m_brush, rectangle3, m_sf);
			rectangle.Y += m_item_height;
			rectangle2.Y += m_item_height;
			rectangle3.Y += m_item_height;
			m_brush.Color = m_clr_item_2;
			graphics.FillRectangle(m_brush, rectangle);
			m_brush.Color = ForeColor;
			graphics.DrawString(m_KeysString[2], Font, m_brush, rectangle2, m_sf);
			m_brush.Color = color;
			graphics.DrawString(node_attribute.Link, Font, Brushes.CornflowerBlue, rectangle3, m_sf);
			if (!string.IsNullOrEmpty(node_attribute.Link))
			{
				m_rect_link = rectangle3;
			}
			m_brush.Color = Color.FromArgb(40, 125, 125, 125);
			graphics.FillRectangle(m_brush, 0, _ShowTitle ? m_nTitleHeight : 0, m_nInfoLeft - 1, m_item_height * 3);
			rectangle.X = 5;
			rectangle.Y += m_item_height;
			rectangle.Width = base.Width - 10;
			if (!string.IsNullOrEmpty(m_node_attribute.Description))
			{
				float num = graphics.MeasureString(m_node_attribute.Description, Font, rectangle.Width).Height;
				rectangle.Height = (int)Math.Ceiling(num / (float)m_item_height) * m_item_height;
				m_brush.Color = color;
				m_sf.FormatFlags = (StringFormatFlags)0;
				graphics.DrawString(m_node_attribute.Description, Font, m_brush, rectangle, m_sf);
			}
			m_nInfoVHeight = rectangle.Bottom;
			bool flag = STNodeAttribute.GetHelpMethod(m_type) != null;
			rectangle.X = 5;
			rectangle.Y += rectangle.Height;
			rectangle.Height = m_item_height;
			m_sf.Alignment = StringAlignment.Center;
			m_brush.Color = Color.FromArgb(125, 125, 125, 125);
			graphics.FillRectangle(m_brush, rectangle);
			if (flag)
			{
				m_brush.Color = Color.CornflowerBlue;
			}
			graphics.DrawString(m_KeysString[3], Font, m_brush, rectangle, m_sf);
			if (flag)
			{
				m_rect_help = rectangle;
			}
			else
			{
				int num2 = (int)graphics.MeasureString(m_KeysString[3], Font).Width + 1;
				int num3 = rectangle.X + (rectangle.Width - num2) / 2;
				int num4 = rectangle.Y + rectangle.Height / 2;
				m_pen.Color = m_brush.Color;
				graphics.DrawLine(m_pen, num3, num4, num3 + num2, num4);
			}
			m_nInfoVHeight = rectangle.Bottom;
		}
	}

	protected virtual void OnProcessPropertyMouseDown(MouseEventArgs e)
	{
		bool flag = false;
		if (m_item_selected != m_item_hover)
		{
			m_item_selected = m_item_hover;
			flag = true;
		}
		m_item_down_value = null;
		if (m_item_selected == null)
		{
			if (flag)
			{
				Invalidate();
			}
			return;
		}
		if (m_item_selected.RectangleR.Contains(e.Location))
		{
			m_item_down_value = m_item_selected;
			if (_ReadOnlyModel)
			{
				return;
			}
			m_item_selected.OnMouseDown(e);
		}
		else if (m_item_selected.RectangleL.Contains(e.Location))
		{
			m_str_desc = m_item_selected.Description;
			flag = true;
		}
		if (flag)
		{
			Invalidate();
		}
	}

	protected virtual void OnProcessInfoMouseDown(MouseEventArgs e)
	{
		try
		{
			if (m_rect_link.Contains(e.Location))
			{
				Process.Start(m_node_attribute.Link);
			}
			else if (m_rect_help.Contains(e.Location))
			{
				STNodeAttribute.ShowHelp(m_type);
			}
		}
		catch (Exception ex)
		{
			SetErrorMessage(ex.Message);
		}
	}

	protected virtual void OnProcessPropertyMouseMove(MouseEventArgs e)
	{
		if (m_item_down_value != null)
		{
			m_item_down_value.OnMouseMove(e);
			return;
		}
		STNodePropertyDescriptor sTNodePropertyDescriptor = null;
		foreach (STNodePropertyDescriptor item in m_lst_item)
		{
			if (item.Rectangle.Contains(e.Location))
			{
				sTNodePropertyDescriptor = item;
				break;
			}
		}
		if (sTNodePropertyDescriptor != null)
		{
			if (sTNodePropertyDescriptor.RectangleR.Contains(e.Location))
			{
				if (m_item_hover_value != sTNodePropertyDescriptor)
				{
					if (m_item_hover_value != null)
					{
						m_item_hover_value.OnMouseLeave(e);
					}
					m_item_hover_value = sTNodePropertyDescriptor;
					m_item_hover_value.OnMouseEnter(e);
				}
				m_item_hover_value.OnMouseMove(e);
			}
			else if (m_item_hover_value != null)
			{
				m_item_hover_value.OnMouseLeave(e);
			}
		}
		if (m_item_hover != sTNodePropertyDescriptor)
		{
			m_item_hover = sTNodePropertyDescriptor;
			Invalidate();
		}
	}

	protected virtual void OnProcessHelpMouseMove(MouseEventArgs e)
	{
		if (m_rect_link.Contains(e.Location) || m_rect_help.Contains(e.Location))
		{
			Cursor = Cursors.Hand;
		}
		else
		{
			Cursor = Cursors.Arrow;
		}
	}

	public void SetNode(STNode node)
	{
		if (node == _STNode)
		{
			return;
		}
		m_nInfoOffsetY = (m_nPropertyOffsetY = 0);
		m_nInfoVHeight = (m_nPropertyVHeight = 0);
		m_rect_link = (m_rect_help = Rectangle.Empty);
		m_str_desc = (m_str_err = null);
		_STNode = node;
		if (node != null)
		{
			m_type = node.GetType();
			m_lst_item = GetProperties(node);
			SetItemRectangle();
			m_b_current_draw_info = m_lst_item.Count == 0 || _InfoFirstOnDraw;
			if (_AutoColor)
			{
				_ItemSelectedColor = _STNode.TitleColor;
			}
		}
		else
		{
			m_type = null;
			m_lst_item.Clear();
			m_node_attribute = null;
		}
		Invalidate();
	}

	public void SetInfoKey(string strAuthor, string strMail, string strLink, string strHelp)
	{
		m_KeysString = new string[4] { strAuthor, strMail, strLink, strHelp };
	}

	public void SetErrorMessage(string strText)
	{
		m_str_err = strText;
		Invalidate();
	}
}
