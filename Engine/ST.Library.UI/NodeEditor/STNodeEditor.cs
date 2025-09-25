using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ST.Library.UI.NodeEditor;

public class STNodeEditor : Control
{
	protected enum CanvasAction
	{
		None,
		MoveNode,
		ConnectOption,
		SelectRectangle,
		DrawMarkDetails
	}

	protected struct MagnetInfo
	{
		public bool XMatched;

		public bool YMatched;

		public int X;

		public int Y;

		public int OffsetX;

		public int OffsetY;
	}

	private const uint WM_MOUSEHWHEEL = 526u;

	protected static readonly Type m_type_node = typeof(STNode);

	private float _CanvasOffsetX;

	private float _CanvasOffsetY;

	private PointF _CanvasOffset;

	private Rectangle _CanvasValidBounds;

	private float _CanvasScale = 1f;

	private float _Curvature = 0.3f;

	private bool _ShowMagnet = true;

	private bool _ShowBorder = true;

	private bool _ShowGrid = true;

	private bool _ShowLocation = true;

	private STNodeCollection _Nodes;

	private STNode _ActiveNode;

	private STNode _HoverNode;

	private Color _GridColor = Color.Black;

	private Color _BorderColor = Color.Black;

	private Color _BorderHoverColor = Color.Gray;

	private Color _BorderSelectedColor = Color.Orange;

	private Color _BorderActiveColor = Color.OrangeRed;

	private Color _MarkForeColor = Color.White;

	private Color _MarkBackColor = Color.FromArgb(180, Color.Black);

	private Color _MagnetColor = Color.Lime;

	private Color _SelectedRectangleColor = Color.DodgerBlue;

	private Color _HighLineColor = Color.Cyan;

	private Color _LocationForeColor = Color.Red;

	private Color _LocationBackColor = Color.FromArgb(120, Color.Black);

	private Color _UnknownTypeColor = Color.Gray;

	private Dictionary<Type, Color> _TypeColor = new Dictionary<Type, Color>();

	private bool m_enableEdit;

	protected Point m_pt_in_control;

	protected PointF m_pt_in_canvas;

	protected Point m_pt_down_in_control;

	protected PointF m_pt_down_in_canvas;

	protected PointF m_pt_canvas_old;

	protected Point m_pt_dot_down;

	protected STNodeOption m_option_down;

	protected STNode m_node_down;

	protected bool m_mouse_in_control;

	private DrawingTools m_drawing_tools;

	private NodeFindInfo m_find;

	private MagnetInfo m_mi;

	private RectangleF m_rect_select;

	private Image m_img_border;

	private Image m_img_border_hover;

	private Image m_img_border_selected;

	private Image m_img_border_active;

	private float m_real_canvas_x;

	private float m_real_canvas_y;

	private Dictionary<STNode, Point> m_dic_pt_selected = new Dictionary<STNode, Point>();

	private List<int> m_lst_magnet_x = new List<int>();

	private List<int> m_lst_magnet_y = new List<int>();

	private List<int> m_lst_magnet_mx = new List<int>();

	private List<int> m_lst_magnet_my = new List<int>();

	private DateTime m_dt_vw = DateTime.Now;

	private DateTime m_dt_hw = DateTime.Now;

	private CanvasAction m_ca;

	private HashSet<STNode> m_hs_node_selected = new HashSet<STNode>();

	private bool m_is_process_mouse_event = true;

	private bool m_is_buildpath;

	private Pen m_p_line = new Pen(Color.Cyan, 2f);

	private Pen m_p_line_hover = new Pen(Color.Cyan, 4f);

	private GraphicsPath m_gp_hover;

	private StringFormat m_sf = new StringFormat();

	private Dictionary<GraphicsPath, ConnectionInfo> m_dic_gp_info = new Dictionary<GraphicsPath, ConnectionInfo>();

	private List<Point> m_lst_node_out = new List<Point>();

	private Dictionary<string, Type> m_dic_guid_type = new Dictionary<string, Type>();

	private Dictionary<string, Type> m_dic_model_type = new Dictionary<string, Type>();

	private int m_time_alert;

	private int m_alpha_alert;

	private string m_str_alert;

	private Color m_forecolor_alert;

	private Color m_backcolor_alert;

	private DateTime m_dt_alert;

	private Rectangle m_rect_alert;

	private AlertLocation m_al;

	[Browsable(false)]
	public float CanvasOffsetX => _CanvasOffsetX;

	[Browsable(false)]
	public float CanvasOffsetY => _CanvasOffsetY;

	[Browsable(false)]
	public PointF CanvasOffset
	{
		get
		{
			_CanvasOffset.X = _CanvasOffsetX;
			_CanvasOffset.Y = _CanvasOffsetY;
			return _CanvasOffset;
		}
	}

	[Browsable(false)]
	public Rectangle CanvasValidBounds => _CanvasValidBounds;

	[Browsable(false)]
	public float CanvasScale => _CanvasScale;

	[Browsable(false)]
	public float Curvature
	{
		get
		{
			return _Curvature;
		}
		set
		{
			if (value < 0f)
			{
				value = 0f;
			}
			if (value > 1f)
			{
				value = 1f;
			}
			_Curvature = value;
			if (m_dic_gp_info.Count != 0)
			{
				BuildLinePath();
			}
		}
	}

	[Description("获取或设置移动画布中 Node 时候 是否启用磁铁效果")]
	[DefaultValue(true)]
	public bool ShowMagnet
	{
		get
		{
			return _ShowMagnet;
		}
		set
		{
			_ShowMagnet = value;
		}
	}

	[Description("获取或设置 移动画布中是否显示 Node 边框")]
	[DefaultValue(true)]
	public bool ShowBorder
	{
		get
		{
			return _ShowBorder;
		}
		set
		{
			_ShowBorder = value;
			Invalidate();
		}
	}

	[Description("获取或设置画布中是否绘制背景网格线条")]
	[DefaultValue(true)]
	public bool ShowGrid
	{
		get
		{
			return _ShowGrid;
		}
		set
		{
			_ShowGrid = value;
			Invalidate();
		}
	}

	[Description("获取或设置是否在画布边缘显示超出视角的 Node 位置信息")]
	[DefaultValue(true)]
	public bool ShowLocation
	{
		get
		{
			return _ShowLocation;
		}
		set
		{
			_ShowLocation = value;
			Invalidate();
		}
	}

	[Browsable(false)]
	public STNodeCollection Nodes => _Nodes;

	[Browsable(false)]
	public STNode ActiveNode => _ActiveNode;

	[Browsable(false)]
	public STNode HoverNode => _HoverNode;

	[Description("获取或设置绘制画布背景时 网格线条颜色")]
	[DefaultValue(typeof(Color), "Black")]
	public Color GridColor
	{
		get
		{
			return _GridColor;
		}
		set
		{
			_GridColor = value;
			Invalidate();
		}
	}

	[Description("获取或设置画布中 Node 边框颜色")]
	[DefaultValue(typeof(Color), "Black")]
	public Color BorderColor
	{
		get
		{
			return _BorderColor;
		}
		set
		{
			_BorderColor = value;
			if (m_img_border != null)
			{
				m_img_border.Dispose();
			}
			m_img_border = CreateBorderImage(value);
			Invalidate();
		}
	}

	[Description("获取或设置画布中悬停 Node 边框颜色")]
	[DefaultValue(typeof(Color), "Gray")]
	public Color BorderHoverColor
	{
		get
		{
			return _BorderHoverColor;
		}
		set
		{
			_BorderHoverColor = value;
			if (m_img_border_hover != null)
			{
				m_img_border_hover.Dispose();
			}
			m_img_border_hover = CreateBorderImage(value);
			Invalidate();
		}
	}

	[Description("获取或设置画布中选中 Node 边框颜色")]
	[DefaultValue(typeof(Color), "Orange")]
	public Color BorderSelectedColor
	{
		get
		{
			return _BorderSelectedColor;
		}
		set
		{
			_BorderSelectedColor = value;
			if (m_img_border_selected != null)
			{
				m_img_border_selected.Dispose();
			}
			m_img_border_selected = CreateBorderImage(value);
			Invalidate();
		}
	}

	[Description("获取或设置画布中活动 Node 边框颜色")]
	[DefaultValue(typeof(Color), "OrangeRed")]
	public Color BorderActiveColor
	{
		get
		{
			return _BorderActiveColor;
		}
		set
		{
			_BorderActiveColor = value;
			if (m_img_border_active != null)
			{
				m_img_border_active.Dispose();
			}
			m_img_border_active = CreateBorderImage(value);
			Invalidate();
		}
	}

	[Description("获取或设置画布绘制 Node 标记详情采用的前景色")]
	[DefaultValue(typeof(Color), "White")]
	public Color MarkForeColor
	{
		get
		{
			return _MarkBackColor;
		}
		set
		{
			_MarkBackColor = value;
			Invalidate();
		}
	}

	[Description("获取或设置画布绘制 Node 标记详情采用的背景色")]
	public Color MarkBackColor
	{
		get
		{
			return _MarkBackColor;
		}
		set
		{
			_MarkBackColor = value;
			Invalidate();
		}
	}

	[Description("获取或设置画布中移动 Node 时候 磁铁标记颜色")]
	[DefaultValue(typeof(Color), "Lime")]
	public Color MagnetColor
	{
		get
		{
			return _MagnetColor;
		}
		set
		{
			_MagnetColor = value;
		}
	}

	[Description("获取或设置画布中选择矩形区域的颜色")]
	[DefaultValue(typeof(Color), "DodgerBlue")]
	public Color SelectedRectangleColor
	{
		get
		{
			return _SelectedRectangleColor;
		}
		set
		{
			_SelectedRectangleColor = value;
		}
	}

	[Description("获取或设置画布中高亮连线的颜色")]
	[DefaultValue(typeof(Color), "Cyan")]
	public Color HighLineColor
	{
		get
		{
			return _HighLineColor;
		}
		set
		{
			_HighLineColor = value;
		}
	}

	[Description("获取或设置画布中边缘位置提示区域前景色")]
	[DefaultValue(typeof(Color), "Red")]
	public Color LocationForeColor
	{
		get
		{
			return _LocationForeColor;
		}
		set
		{
			_LocationForeColor = value;
			Invalidate();
		}
	}

	[Description("获取或设置画布中边缘位置提示区域背景色")]
	public Color LocationBackColor
	{
		get
		{
			return _LocationBackColor;
		}
		set
		{
			_LocationBackColor = value;
			Invalidate();
		}
	}

	[Description("获取或设置画布中当 Node 中 Option 数据类型无法确定时应当使用的颜色")]
	[DefaultValue(typeof(Color), "Gray")]
	public Color UnknownTypeColor
	{
		get
		{
			return _UnknownTypeColor;
		}
		set
		{
			_UnknownTypeColor = value;
			Invalidate();
		}
	}

	[Browsable(false)]
	public Dictionary<Type, Color> TypeColor => _TypeColor;

	[Browsable(false)]
	public bool EnableEdit
	{
		get
		{
			return m_enableEdit;
		}
		set
		{
			m_enableEdit = value;
		}
	}

	[Description("活动的节点发生变化时候发生")]
	public event EventHandler ActiveChanged;

	[Description("选择的节点发生变化时候发生")]
	public event EventHandler SelectedChanged;

	[Description("悬停的节点发生变化时候发生")]
	public event EventHandler HoverChanged;

	[Description("当节点被添加时候发生")]
	public event STNodeEditorEventHandler NodeAdded;

	[Description("当节点被移除时候发生")]
	public event STNodeEditorEventHandler NodeRemoved;

	[Description("移动画布原点时候发生")]
	public event EventHandler CanvasMoved;

	[Description("缩放画布时候发生")]
	public event EventHandler CanvasScaled;

	[Description("连接节点选项时候发生")]
	public event STNodeEditorOptionEventHandler OptionConnected;

	[Description("正在连接节点选项时候发生")]
	public event STNodeEditorOptionEventHandler OptionConnecting;

	[Description("断开节点选项时候发生")]
	public event STNodeEditorOptionEventHandler OptionDisConnected;

	[Description("正在断开节点选项时候发生")]
	public event STNodeEditorOptionEventHandler OptionDisConnecting;

	public STNodeEditor()
	{
		SetStyle(ControlStyles.UserPaint, value: true);
		SetStyle(ControlStyles.ResizeRedraw, value: true);
		SetStyle(ControlStyles.AllPaintingInWmPaint, value: true);
		SetStyle(ControlStyles.OptimizedDoubleBuffer, value: true);
		SetStyle(ControlStyles.SupportsTransparentBackColor, value: true);
		_Nodes = new STNodeCollection(this);
		BackColor = Color.FromArgb(255, 34, 34, 34);
		MinimumSize = new Size(100, 100);
		base.Size = new Size(200, 200);
		AllowDrop = true;
		m_enableEdit = true;
		m_real_canvas_x = (_CanvasOffsetX = 10f);
		m_real_canvas_y = (_CanvasOffsetY = 10f);
	}

	protected internal virtual void OnSelectedChanged(EventArgs e)
	{
		if (this.SelectedChanged != null)
		{
			this.SelectedChanged(this, e);
		}
	}

	protected virtual void OnActiveChanged(EventArgs e)
	{
		if (this.ActiveChanged != null)
		{
			this.ActiveChanged(this, e);
		}
	}

	protected virtual void OnHoverChanged(EventArgs e)
	{
		if (this.HoverChanged != null)
		{
			this.HoverChanged(this, e);
		}
	}

	protected internal virtual void OnNodeAdded(STNodeEditorEventArgs e)
	{
		if (this.NodeAdded != null)
		{
			this.NodeAdded(this, e);
		}
	}

	protected internal virtual void OnNodeRemoved(STNodeEditorEventArgs e)
	{
		if (this.NodeRemoved != null)
		{
			this.NodeRemoved(this, e);
		}
	}

	protected virtual void OnCanvasMoved(EventArgs e)
	{
		if (this.CanvasMoved != null)
		{
			this.CanvasMoved(this, e);
		}
	}

	protected virtual void OnCanvasScaled(EventArgs e)
	{
		if (this.CanvasScaled != null)
		{
			this.CanvasScaled(this, e);
		}
	}

	protected internal virtual void OnOptionConnected(STNodeEditorOptionEventArgs e)
	{
		if (this.OptionConnected != null)
		{
			this.OptionConnected(this, e);
		}
	}

	protected internal virtual void OnOptionDisConnected(STNodeEditorOptionEventArgs e)
	{
		if (this.OptionDisConnected != null)
		{
			this.OptionDisConnected(this, e);
		}
	}

	protected internal virtual void OnOptionConnecting(STNodeEditorOptionEventArgs e)
	{
		if (this.OptionConnecting != null)
		{
			this.OptionConnecting(this, e);
		}
	}

	protected internal virtual void OnOptionDisConnecting(STNodeEditorOptionEventArgs e)
	{
		if (this.OptionDisConnecting != null)
		{
			this.OptionDisConnecting(this, e);
		}
	}

	protected override void OnCreateControl()
	{
		m_drawing_tools = new DrawingTools
		{
			Pen = new Pen(Color.Black, 1f),
			SolidBrush = new SolidBrush(Color.Black)
		};
		m_img_border = CreateBorderImage(_BorderColor);
		m_img_border_active = CreateBorderImage(_BorderActiveColor);
		m_img_border_hover = CreateBorderImage(_BorderHoverColor);
		m_img_border_selected = CreateBorderImage(_BorderSelectedColor);
		base.OnCreateControl();
		Thread thread = new Thread(MoveCanvasThread);
		thread.IsBackground = true;
		thread.Start();
		Thread thread2 = new Thread(ShowAlertThread);
		thread2.IsBackground = true;
		thread2.Start();
		m_sf = new StringFormat();
		m_sf.Alignment = StringAlignment.Near;
		m_sf.FormatFlags = StringFormatFlags.NoWrap;
		m_sf.SetTabStops(0f, new float[1] { 40f });
	}

	protected override void WndProc(ref Message m)
	{
		base.WndProc(ref m);
		try
		{
			Point p = new Point((int)m.LParam >> 16, (ushort)(int)m.LParam);
			p = PointToClient(p);
			if ((long)m.Msg == 526)
			{
				MouseButtons mouseButtons = MouseButtons.None;
				int num = (ushort)(int)m.WParam;
				if ((num & 1) == 1)
				{
					mouseButtons |= MouseButtons.Left;
				}
				if ((num & 0x10) == 16)
				{
					mouseButtons |= MouseButtons.Middle;
				}
				if ((num & 2) == 2)
				{
					mouseButtons |= MouseButtons.Right;
				}
				if ((num & 0x20) == 32)
				{
					mouseButtons |= MouseButtons.XButton1;
				}
				if ((num & 0x40) == 64)
				{
					mouseButtons |= MouseButtons.XButton2;
				}
				OnMouseHWheel(new MouseEventArgs(mouseButtons, 0, p.X, p.Y, (int)m.WParam >> 16));
			}
		}
		catch
		{
		}
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		Graphics graphics = e.Graphics;
		graphics.Clear(BackColor);
		graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
		m_drawing_tools.Graphics = graphics;
		SolidBrush solidBrush = m_drawing_tools.SolidBrush;
		if (_ShowGrid)
		{
			OnDrawGrid(m_drawing_tools, base.Width, base.Height);
		}
		graphics.TranslateTransform(_CanvasOffsetX, _CanvasOffsetY);
		graphics.ScaleTransform(_CanvasScale, _CanvasScale);
		OnDrawConnectedLine(m_drawing_tools);
		OnDrawNode(m_drawing_tools, ControlToCanvas(base.ClientRectangle));
		if (m_ca == CanvasAction.ConnectOption)
		{
			m_drawing_tools.Pen.Color = _HighLineColor;
			graphics.SmoothingMode = SmoothingMode.HighQuality;
			if (m_option_down.IsInput)
			{
				DrawBezier(graphics, m_drawing_tools.Pen, m_pt_in_canvas, m_pt_dot_down, _Curvature);
			}
			else
			{
				DrawBezier(graphics, m_drawing_tools.Pen, m_pt_dot_down, m_pt_in_canvas, _Curvature);
			}
		}
		graphics.ResetTransform();
		switch (m_ca)
		{
		case CanvasAction.MoveNode:
			if (_ShowMagnet && _ActiveNode != null)
			{
				OnDrawMagnet(m_drawing_tools, m_mi);
			}
			break;
		case CanvasAction.SelectRectangle:
			OnDrawSelectedRectangle(m_drawing_tools, CanvasToControl(m_rect_select));
			break;
		case CanvasAction.DrawMarkDetails:
			if (!string.IsNullOrEmpty(m_find.Mark))
			{
				OnDrawMark(m_drawing_tools);
			}
			break;
		}
		if (_ShowLocation)
		{
			OnDrawNodeOutLocation(m_drawing_tools, base.Size, m_lst_node_out);
		}
		OnDrawAlert(graphics);
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		base.OnMouseDown(e);
		Focus();
		m_ca = CanvasAction.None;
		m_mi.XMatched = (m_mi.YMatched = false);
		m_pt_down_in_control = e.Location;
		m_pt_down_in_canvas.X = ((float)e.X - _CanvasOffsetX) / _CanvasScale;
		m_pt_down_in_canvas.Y = ((float)e.Y - _CanvasOffsetY) / _CanvasScale;
		m_pt_canvas_old.X = _CanvasOffsetX;
		m_pt_canvas_old.Y = _CanvasOffsetY;
		if (m_gp_hover != null && e.Button == MouseButtons.Right)
		{
			if (m_enableEdit)
			{
				DisConnectionHover();
				m_is_process_mouse_event = false;
			}
			return;
		}
		NodeFindInfo graphics = FindNodeFromPoint(m_pt_down_in_canvas);
		if (!string.IsNullOrEmpty(graphics.Mark))
		{
			m_ca = CanvasAction.DrawMarkDetails;
			Invalidate();
		}
		else if (graphics.NodeOption != null)
		{
			if (m_enableEdit)
			{
				StartConnect(graphics.NodeOption);
			}
		}
		else if (graphics.Node != null)
		{
			graphics.Node.OnMouseDown(new MouseEventArgs(e.Button, e.Clicks, (int)m_pt_down_in_canvas.X - graphics.Node.Left, (int)m_pt_down_in_canvas.Y - graphics.Node.Top, e.Delta));
			if ((Control.ModifierKeys & Keys.Control) == Keys.Control)
			{
				if (graphics.Node.IsSelected)
				{
					if (graphics.Node == _ActiveNode)
					{
						SetActiveNode(null);
					}
				}
				else
				{
					graphics.Node.SetSelected(bSelected: true, bRedraw: true);
				}
				return;
			}
			if (!graphics.Node.IsSelected)
			{
				STNode[] array = m_hs_node_selected.ToArray();
				foreach (STNode sTNode in array)
				{
					sTNode.SetSelected(bSelected: false, bRedraw: false);
				}
			}
			graphics.Node.SetSelected(bSelected: true, bRedraw: false);
			SetActiveNode(graphics.Node);
			if (PointInRectangle(graphics.Node.Rectangle, m_pt_down_in_canvas.X, m_pt_down_in_canvas.Y))
			{
				if (e.Button == MouseButtons.Right)
				{
					if (graphics.Node.ContextMenuStrip != null)
					{
						graphics.Node.ContextMenuStrip.Show(PointToScreen(e.Location));
					}
					return;
				}
				m_dic_pt_selected.Clear();
				lock (m_hs_node_selected)
				{
					foreach (STNode item in m_hs_node_selected)
					{
						m_dic_pt_selected.Add(item, item.Location);
					}
				}
				m_ca = CanvasAction.MoveNode;
				if (_ShowMagnet && _ActiveNode != null)
				{
					BuildMagnetLocation();
				}
			}
			else
			{
				m_node_down = graphics.Node;
			}
		}
		else
		{
			SetActiveNode(null);
			STNode[] array2 = m_hs_node_selected.ToArray();
			foreach (STNode sTNode2 in array2)
			{
				sTNode2.SetSelected(bSelected: false, bRedraw: false);
			}
			m_ca = CanvasAction.SelectRectangle;
			ref RectangleF rect_select = ref m_rect_select;
			float num = (m_rect_select.Height = 0f);
			rect_select.Width = num;
			m_node_down = null;
		}
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		m_pt_in_control = e.Location;
		m_pt_in_canvas.X = ((float)e.X - _CanvasOffsetX) / _CanvasScale;
		m_pt_in_canvas.Y = ((float)e.Y - _CanvasOffsetY) / _CanvasScale;
		if (m_node_down != null)
		{
			m_node_down.OnMouseMove(new MouseEventArgs(e.Button, e.Clicks, (int)m_pt_in_canvas.X - m_node_down.Left, (int)m_pt_in_canvas.Y - m_node_down.Top, e.Delta));
			return;
		}
		if (e.Button == MouseButtons.Middle)
		{
			_CanvasOffsetX = (m_real_canvas_x = m_pt_canvas_old.X + (float)(e.X - m_pt_down_in_control.X));
			_CanvasOffsetY = (m_real_canvas_y = m_pt_canvas_old.Y + (float)(e.Y - m_pt_down_in_control.Y));
			Invalidate();
			return;
		}
		if (e.Button == MouseButtons.Left)
		{
			m_gp_hover = null;
			switch (m_ca)
			{
			case CanvasAction.MoveNode:
				if (m_enableEdit)
				{
					MoveNode(e.Location);
				}
				return;
			case CanvasAction.ConnectOption:
				Invalidate();
				return;
			case CanvasAction.SelectRectangle:
				m_rect_select.X = ((m_pt_down_in_canvas.X < m_pt_in_canvas.X) ? m_pt_down_in_canvas.X : m_pt_in_canvas.X);
				m_rect_select.Y = ((m_pt_down_in_canvas.Y < m_pt_in_canvas.Y) ? m_pt_down_in_canvas.Y : m_pt_in_canvas.Y);
				m_rect_select.Width = Math.Abs(m_pt_in_canvas.X - m_pt_down_in_canvas.X);
				m_rect_select.Height = Math.Abs(m_pt_in_canvas.Y - m_pt_down_in_canvas.Y);
				foreach (STNode node in _Nodes)
				{
					node.SetSelected(m_rect_select.IntersectsWith(node.Rectangle), bRedraw: false);
				}
				Invalidate();
				return;
			}
		}
		NodeFindInfo nodeFindInfo = FindNodeFromPoint(m_pt_in_canvas);
		bool flag = false;
		if (_HoverNode != nodeFindInfo.Node)
		{
			if (nodeFindInfo.Node != null)
			{
				nodeFindInfo.Node.OnMouseEnter(EventArgs.Empty);
			}
			if (_HoverNode != null)
			{
				_HoverNode.OnMouseLeave(new MouseEventArgs(e.Button, e.Clicks, (int)m_pt_in_canvas.X - _HoverNode.Left, (int)m_pt_in_canvas.Y - _HoverNode.Top, e.Delta));
			}
			_HoverNode = nodeFindInfo.Node;
			OnHoverChanged(EventArgs.Empty);
			flag = true;
		}
		if (_HoverNode != null)
		{
			_HoverNode.OnMouseMove(new MouseEventArgs(e.Button, e.Clicks, (int)m_pt_in_canvas.X - _HoverNode.Left, (int)m_pt_in_canvas.Y - _HoverNode.Top, e.Delta));
			m_gp_hover = null;
		}
		else
		{
			GraphicsPath graphicsPath = null;
			foreach (KeyValuePair<GraphicsPath, ConnectionInfo> item in m_dic_gp_info)
			{
				if (item.Key.IsOutlineVisible(m_pt_in_canvas, m_p_line_hover))
				{
					graphicsPath = item.Key;
					break;
				}
			}
			if (m_gp_hover != graphicsPath)
			{
				m_gp_hover = graphicsPath;
				flag = true;
			}
		}
		if (flag)
		{
			Invalidate();
		}
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		base.OnMouseUp(e);
		NodeFindInfo nodeFindInfo = FindNodeFromPoint(m_pt_in_canvas);
		switch (m_ca)
		{
		case CanvasAction.MoveNode:
			foreach (STNode item in m_dic_pt_selected.Keys.ToList())
			{
				m_dic_pt_selected[item] = item.Location;
			}
			break;
		case CanvasAction.ConnectOption:
			if (!(e.Location == m_pt_down_in_control) && nodeFindInfo.NodeOption != null)
			{
				if (m_option_down.IsInput)
				{
					nodeFindInfo.NodeOption.ConnectOption(m_option_down);
				}
				else
				{
					m_option_down.ConnectOption(nodeFindInfo.NodeOption);
				}
			}
			break;
		}
		if (m_is_process_mouse_event && _ActiveNode != null)
		{
			MouseEventArgs e2 = new MouseEventArgs(e.Button, e.Clicks, (int)m_pt_in_canvas.X - _ActiveNode.Left, (int)m_pt_in_canvas.Y - _ActiveNode.Top, e.Delta);
			_ActiveNode.OnMouseUp(e2);
			m_node_down = null;
		}
		m_is_process_mouse_event = true;
		m_ca = CanvasAction.None;
		Invalidate();
	}

	protected override void OnMouseEnter(EventArgs e)
	{
		base.OnMouseEnter(e);
		m_mouse_in_control = true;
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		base.OnMouseLeave(e);
		m_mouse_in_control = false;
		if (_HoverNode != null)
		{
			_HoverNode.OnMouseLeave(e);
		}
		_HoverNode = null;
		Invalidate();
	}

	protected override void OnMouseWheel(MouseEventArgs e)
	{
		base.OnMouseWheel(e);
		if ((Control.ModifierKeys & Keys.Control) == Keys.Control)
		{
			float f = _CanvasScale + ((e.Delta < 0) ? (-0.1f) : 0.1f);
			ScaleCanvas(f, base.Width / 2, base.Height / 2);
		}
		else if (m_mouse_in_control)
		{
			NodeFindInfo nodeFindInfo = FindNodeFromPoint(m_pt_in_canvas);
			if (_HoverNode != null)
			{
				_HoverNode.OnMouseWheel(new MouseEventArgs(e.Button, e.Clicks, (int)m_pt_in_canvas.X - _HoverNode.Left, (int)m_pt_in_canvas.Y - _HoverNode.Top, e.Delta));
			}
		}
	}

	protected virtual void OnMouseHWheel(MouseEventArgs e)
	{
		if ((Control.ModifierKeys & Keys.Control) != Keys.Control && m_mouse_in_control && _HoverNode != null)
		{
			_HoverNode.OnMouseWheel(new MouseEventArgs(e.Button, e.Clicks, (int)m_pt_in_canvas.X - _HoverNode.Left, (int)m_pt_in_canvas.Y - _HoverNode.Top, e.Delta));
		}
	}

	protected override void OnMouseClick(MouseEventArgs e)
	{
		base.OnMouseClick(e);
		if (_ActiveNode != null && m_is_process_mouse_event && PointInRectangle(_ActiveNode.Rectangle, m_pt_in_canvas.X, m_pt_in_canvas.Y))
		{
			_ActiveNode.OnMouseClick(new MouseEventArgs(e.Button, e.Clicks, (int)m_pt_down_in_canvas.X - _ActiveNode.Left, (int)m_pt_down_in_canvas.Y - _ActiveNode.Top, e.Delta));
		}
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		base.OnKeyDown(e);
		if (_ActiveNode != null)
		{
			_ActiveNode.OnKeyDown(e);
		}
	}

	protected override void OnKeyUp(KeyEventArgs e)
	{
		base.OnKeyUp(e);
		if (_ActiveNode != null)
		{
			_ActiveNode.OnKeyUp(e);
		}
		m_node_down = null;
	}

	protected override void OnKeyPress(KeyPressEventArgs e)
	{
		base.OnKeyPress(e);
		if (_ActiveNode != null)
		{
			_ActiveNode.OnKeyPress(e);
		}
	}

	protected override void OnDragEnter(DragEventArgs drgevent)
	{
		base.OnDragEnter(drgevent);
		if (!base.DesignMode)
		{
			if (drgevent.Data.GetDataPresent("STNodeType"))
			{
				drgevent.Effect = DragDropEffects.Copy;
			}
			else
			{
				drgevent.Effect = DragDropEffects.None;
			}
		}
	}

	protected override void OnDragDrop(DragEventArgs drgevent)
	{
		base.OnDragDrop(drgevent);
		if (base.DesignMode || !drgevent.Data.GetDataPresent("STNodeType"))
		{
			return;
		}
		object data = drgevent.Data.GetData("STNodeType");
		if (data is Type)
		{
			Type type = (Type)data;
			if (type.IsSubclassOf(typeof(STNode)))
			{
				STNode sTNode = (STNode)Activator.CreateInstance(type);
				sTNode.Create();
				Point p = new Point(drgevent.X, drgevent.Y);
				p = PointToClient(p);
				p = ControlToCanvas(p);
				sTNode.Left = p.X;
				sTNode.Top = p.Y;
				Nodes.Add(sTNode);
			}
		}
	}

	protected virtual void OnDrawGrid(DrawingTools dt, int nWidth, int nHeight)
	{
		Graphics graphics = dt.Graphics;
		using Pen pen = new Pen(Color.FromArgb(65, _GridColor));
		using Pen pen2 = new Pen(Color.FromArgb(30, _GridColor));
		float num = 20f * _CanvasScale;
		int num2 = 5 - (int)(_CanvasOffsetX / num);
		for (float num3 = _CanvasOffsetX % num; num3 < (float)nWidth; num3 += num)
		{
			graphics.DrawLine((num2++ % 5 == 0) ? pen : pen2, num3, 0f, num3, nHeight);
		}
		num2 = 5 - (int)(_CanvasOffsetY / num);
		for (float num4 = _CanvasOffsetY % num; num4 < (float)nHeight; num4 += num)
		{
			graphics.DrawLine((num2++ % 5 == 0) ? pen : pen2, 0f, num4, nWidth, num4);
		}
		pen2.Color = Color.FromArgb((_Nodes.Count == 0) ? 255 : 120, _GridColor);
		graphics.DrawLine(pen2, _CanvasOffsetX, 0f, _CanvasOffsetX, nHeight);
		graphics.DrawLine(pen2, 0f, _CanvasOffsetY, nWidth, _CanvasOffsetY);
	}

	protected virtual void OnDrawNode(DrawingTools dt, Rectangle rect)
	{
		m_lst_node_out.Clear();
		foreach (STNode node in _Nodes)
		{
			if (_ShowBorder)
			{
				OnDrawNodeBorder(dt, node);
			}
			node.OnDrawNode(dt);
			if (!string.IsNullOrEmpty(node.Mark))
			{
				node.OnDrawMark(dt);
			}
			if (!rect.IntersectsWith(node.Rectangle))
			{
				m_lst_node_out.Add(node.Location);
			}
		}
	}

	protected virtual void OnDrawNodeBorder(DrawingTools dt, STNode node)
	{
		Image g = null;
		g = ((_ActiveNode == node) ? m_img_border_active : (node.IsSelected ? m_img_border_selected : ((_HoverNode != node) ? m_img_border : m_img_border_hover)));
		RenderBorder(dt.Graphics, node.Rectangle, g);
		if (!string.IsNullOrEmpty(node.Mark))
		{
			RenderBorder(dt.Graphics, node.MarkRectangle, g);
		}
	}

	protected virtual void OnDrawConnectedLine(DrawingTools dt)
	{
		Graphics sTNodeHubOption = dt.Graphics;
		sTNodeHubOption.SmoothingMode = SmoothingMode.HighQuality;
		m_p_line_hover.Color = Color.FromArgb(50, 0, 0, 0);
		Type sTNodeHubOption2 = typeof(object);
		foreach (STNode node in _Nodes)
		{
			foreach (STNodeOption outputOption in node.OutputOptions)
			{
				if (outputOption == STNodeOption.Empty)
				{
					continue;
				}
				if (outputOption.DotColor != Color.Transparent)
				{
					m_p_line.Color = outputOption.DotColor;
				}
				else if (outputOption.DataType == sTNodeHubOption2)
				{
					m_p_line.Color = _UnknownTypeColor;
				}
				else
				{
					m_p_line.Color = (_TypeColor.ContainsKey(outputOption.DataType) ? _TypeColor[outputOption.DataType] : _UnknownTypeColor);
				}
				foreach (STNodeOption item in outputOption.ConnectedOption)
				{
					DrawBezier(sTNodeHubOption, m_p_line_hover, outputOption.DotLeft + outputOption.DotSize, outputOption.DotTop + outputOption.DotSize / 2, item.DotLeft - 1, item.DotTop + item.DotSize / 2, _Curvature);
					DrawBezier(sTNodeHubOption, m_p_line, outputOption.DotLeft + outputOption.DotSize, outputOption.DotTop + outputOption.DotSize / 2, item.DotLeft - 1, item.DotTop + item.DotSize / 2, _Curvature);
					if (m_is_buildpath)
					{
						GraphicsPath key = CreateBezierPath(outputOption.DotLeft + outputOption.DotSize, outputOption.DotTop + outputOption.DotSize / 2, item.DotLeft - 1, item.DotTop + item.DotSize / 2, _Curvature);
						m_dic_gp_info.Add(key, new ConnectionInfo
						{
							Output = outputOption,
							Input = item
						});
					}
				}
			}
		}
		m_p_line_hover.Color = _HighLineColor;
		if (m_gp_hover != null)
		{
			sTNodeHubOption.DrawPath(m_p_line_hover, m_gp_hover);
		}
		m_is_buildpath = false;
	}

	protected virtual void OnDrawMark(DrawingTools dt)
	{
		Graphics sTNodeOption = dt.Graphics;
		SizeF index = sTNodeOption.MeasureString(m_find.Mark, Font);
		Rectangle rectangle = new Rectangle(m_pt_in_control.X + 15, m_pt_in_control.Y + 10, (int)index.Width + 6, 4 + (Font.Height + 4) * m_find.MarkLines.Length);
		if (rectangle.Right > base.Width)
		{
			rectangle.X = base.Width - rectangle.Width;
		}
		if (rectangle.Bottom > base.Height)
		{
			rectangle.Y = base.Height - rectangle.Height;
		}
		if (rectangle.X < 0)
		{
			rectangle.X = 0;
		}
		if (rectangle.Y < 0)
		{
			rectangle.Y = 0;
		}
		dt.SolidBrush.Color = _MarkBackColor;
		sTNodeOption.SmoothingMode = SmoothingMode.None;
		sTNodeOption.FillRectangle(dt.SolidBrush, rectangle);
		rectangle.Width--;
		rectangle.Height--;
		dt.Pen.Color = Color.FromArgb(255, _MarkBackColor);
		sTNodeOption.DrawRectangle(dt.Pen, rectangle);
		dt.SolidBrush.Color = _MarkForeColor;
		m_sf.LineAlignment = StringAlignment.Center;
		rectangle.X += 2;
		rectangle.Width -= 3;
		rectangle.Height = Font.Height + 4;
		int num = rectangle.Y + 2;
		for (int i = 0; i < m_find.MarkLines.Length; i++)
		{
			rectangle.Y = num + i * (Font.Height + 4);
			sTNodeOption.DrawString(m_find.MarkLines[i], Font, dt.SolidBrush, rectangle, m_sf);
		}
	}

	protected virtual void OnDrawMagnet(DrawingTools dt, MagnetInfo mi)
	{
		if (_ActiveNode == null)
		{
			return;
		}
		Graphics sTNodeOption = dt.Graphics;
		Pen index = m_drawing_tools.Pen;
		SolidBrush typeFromHandle = dt.SolidBrush;
		index.Color = _MagnetColor;
		typeFromHandle.Color = Color.FromArgb(_MagnetColor.A / 3, _MagnetColor);
		sTNodeOption.SmoothingMode = SmoothingMode.None;
		int left = _ActiveNode.Left;
		int num = _ActiveNode.Left + _ActiveNode.Width / 2;
		int right = _ActiveNode.Right;
		int top = _ActiveNode.Top;
		int num2 = _ActiveNode.Top + _ActiveNode.Height / 2;
		int bottom = _ActiveNode.Bottom;
		if (mi.XMatched)
		{
			sTNodeOption.DrawLine(index, CanvasToControl(mi.X, isX: true), 0f, CanvasToControl(mi.X, isX: true), base.Height);
		}
		if (mi.YMatched)
		{
			sTNodeOption.DrawLine(index, 0f, CanvasToControl(mi.Y, isX: false), base.Width, CanvasToControl(mi.Y, isX: false));
		}
		sTNodeOption.TranslateTransform(_CanvasOffsetX, _CanvasOffsetY);
		sTNodeOption.ScaleTransform(_CanvasScale, _CanvasScale);
		if (mi.XMatched)
		{
			foreach (STNode node in _Nodes)
			{
				if (node.Left == mi.X || node.Right == mi.X || node.Left + node.Width / 2 == mi.X)
				{
					sTNodeOption.FillRectangle(typeFromHandle, node.Rectangle);
				}
			}
		}
		if (mi.YMatched)
		{
			foreach (STNode node2 in _Nodes)
			{
				if (node2.Top == mi.Y || node2.Bottom == mi.Y || node2.Top + node2.Height / 2 == mi.Y)
				{
					sTNodeOption.FillRectangle(typeFromHandle, node2.Rectangle);
				}
			}
		}
		sTNodeOption.ResetTransform();
	}

	protected virtual void OnDrawSelectedRectangle(DrawingTools dt, RectangleF rectf)
	{
		Graphics sTNodeOption = dt.Graphics;
		SolidBrush index = dt.SolidBrush;
		dt.Pen.Color = _SelectedRectangleColor;
		sTNodeOption.DrawRectangle(dt.Pen, rectf.Left, rectf.Y, rectf.Width, rectf.Height);
		index.Color = Color.FromArgb(_SelectedRectangleColor.A / 3, _SelectedRectangleColor);
		sTNodeOption.FillRectangle(index, CanvasToControl(m_rect_select));
	}

	protected virtual void OnDrawNodeOutLocation(DrawingTools dt, Size sz, List<Point> lstPts)
	{
		Graphics option = dt.Graphics;
		SolidBrush index = dt.SolidBrush;
		index.Color = _LocationBackColor;
		option.SmoothingMode = SmoothingMode.None;
		if (lstPts.Count == _Nodes.Count && _Nodes.Count != 0)
		{
			option.FillRectangle(index, CanvasToControl(_CanvasValidBounds));
		}
		option.FillRectangle(index, 0, 0, 4, sz.Height);
		option.FillRectangle(index, sz.Width - 4, 0, 4, sz.Height);
		option.FillRectangle(index, 4, 0, sz.Width - 8, 4);
		option.FillRectangle(index, 4, sz.Height - 4, sz.Width - 8, 4);
		index.Color = _LocationForeColor;
		foreach (Point lstPt in lstPts)
		{
			Point point = CanvasToControl(lstPt);
			if (point.X < 0)
			{
				point.X = 0;
			}
			if (point.Y < 0)
			{
				point.Y = 0;
			}
			if (point.X > sz.Width)
			{
				point.X = sz.Width - 4;
			}
			if (point.Y > sz.Height)
			{
				point.Y = sz.Height - 4;
			}
			option.FillRectangle(index, point.X, point.Y, 4, 4);
		}
	}

	protected virtual void OnDrawAlert(DrawingTools dt, Rectangle rect, string strText, Color foreColor, Color backColor, AlertLocation al)
	{
		if (m_alpha_alert != 0)
		{
			Graphics sTNodeOption = dt.Graphics;
			SolidBrush index = dt.SolidBrush;
			sTNodeOption.SmoothingMode = SmoothingMode.None;
			index.Color = backColor;
			dt.Pen.Color = index.Color;
			sTNodeOption.FillRectangle(index, rect);
			sTNodeOption.DrawRectangle(dt.Pen, rect.Left, rect.Top, rect.Width - 1, rect.Height - 1);
			index.Color = foreColor;
			m_sf.Alignment = StringAlignment.Center;
			m_sf.LineAlignment = StringAlignment.Center;
			sTNodeOption.SmoothingMode = SmoothingMode.HighQuality;
			sTNodeOption.DrawString(strText, Font, index, rect, m_sf);
		}
	}

	protected virtual Rectangle GetAlertRectangle(Graphics g, string strText, AlertLocation al)
	{
		SizeF sizeF = g.MeasureString(m_str_alert, Font);
		Size size = new Size((int)Math.Round(sizeF.Width + 10f), (int)Math.Round(sizeF.Height + 4f));
		Rectangle result = new Rectangle(4, base.Height - size.Height - 4, size.Width, size.Height);
		switch (al)
		{
		case AlertLocation.Left:
			result.Y = base.Height - size.Height >> 1;
			break;
		case AlertLocation.Top:
			result.Y = 4;
			result.X = base.Width - size.Width >> 1;
			break;
		case AlertLocation.Right:
			result.X = base.Width - size.Width - 4;
			result.Y = base.Height - size.Height >> 1;
			break;
		case AlertLocation.Bottom:
			result.X = base.Width - size.Width >> 1;
			break;
		case AlertLocation.Center:
			result.X = base.Width - size.Width >> 1;
			result.Y = base.Height - size.Height >> 1;
			break;
		case AlertLocation.LeftTop:
		{
			int num = (result.Y = 4);
			result.X = num;
			break;
		}
		case AlertLocation.RightTop:
			result.Y = 4;
			result.X = base.Width - size.Width - 4;
			break;
		case AlertLocation.RightBottom:
			result.X = base.Width - size.Width - 4;
			break;
		}
		return result;
	}

	internal void BuildLinePath()
	{
		foreach (KeyValuePair<GraphicsPath, ConnectionInfo> item in m_dic_gp_info)
		{
			item.Key.Dispose();
		}
		m_dic_gp_info.Clear();
		m_is_buildpath = true;
		Invalidate();
	}

	internal void OnDrawAlert(Graphics g)
	{
		m_rect_alert = GetAlertRectangle(g, m_str_alert, m_al);
		Color foreColor = Color.FromArgb((int)((float)m_alpha_alert / 255f * (float)(int)m_forecolor_alert.A), m_forecolor_alert);
		Color backColor = Color.FromArgb((int)((float)m_alpha_alert / 255f * (float)(int)m_backcolor_alert.A), m_backcolor_alert);
		OnDrawAlert(m_drawing_tools, m_rect_alert, m_str_alert, foreColor, backColor, m_al);
	}

	internal void InternalAddSelectedNode(STNode node)
	{
		node.IsSelected = true;
		lock (m_hs_node_selected)
		{
			m_hs_node_selected.Add(node);
		}
	}

	internal void InternalRemoveSelectedNode(STNode node)
	{
		node.IsSelected = false;
		lock (m_hs_node_selected)
		{
			m_hs_node_selected.Remove(node);
		}
	}

	private void MoveCanvasThread()
	{
		while (true)
		{
			bool flag = false;
			if (m_real_canvas_x != _CanvasOffsetX)
			{
				float num = m_real_canvas_x - _CanvasOffsetX;
				float num2 = Math.Abs(num) / 10f;
				float num3 = Math.Abs(num);
				if (num3 <= 4f)
				{
					num2 = 1f;
				}
				else if (num3 <= 12f)
				{
					num2 = 2f;
				}
				else if (num3 <= 30f)
				{
					num2 = 3f;
				}
				if (num3 < 1f)
				{
					_CanvasOffsetX = m_real_canvas_x;
				}
				else
				{
					_CanvasOffsetX += ((num > 0f) ? num2 : (0f - num2));
				}
				flag = true;
			}
			if (m_real_canvas_y != _CanvasOffsetY)
			{
				float num4 = m_real_canvas_y - _CanvasOffsetY;
				float num5 = Math.Abs(num4) / 10f;
				float num6 = Math.Abs(num4);
				if (num6 <= 4f)
				{
					num5 = 1f;
				}
				else if (num6 <= 12f)
				{
					num5 = 2f;
				}
				else if (num6 <= 30f)
				{
					num5 = 3f;
				}
				if (num6 < 1f)
				{
					_CanvasOffsetY = m_real_canvas_y;
				}
				else
				{
					_CanvasOffsetY += ((num4 > 0f) ? num5 : (0f - num5));
				}
				flag = true;
			}
			if (flag)
			{
				m_pt_canvas_old.X = _CanvasOffsetX;
				m_pt_canvas_old.Y = _CanvasOffsetY;
				Invalidate();
				Thread.Sleep(30);
			}
			else
			{
				Thread.Sleep(100);
			}
		}
	}

	private void ShowAlertThread()
	{
		while (true)
		{
			int num = m_time_alert - (int)DateTime.Now.Subtract(m_dt_alert).TotalMilliseconds;
			if (num > 0)
			{
				Thread.Sleep(num);
			}
			else if (num < -1000)
			{
				if (m_alpha_alert != 0)
				{
					m_alpha_alert = 0;
					Invalidate();
				}
				Thread.Sleep(100);
			}
			else
			{
				m_alpha_alert = (int)(255f - (float)(-num) / 1000f * 255f);
				Invalidate(m_rect_alert);
				Thread.Sleep(50);
			}
		}
	}

	private Image CreateBorderImage(Color clr)
	{
		Image image = new Bitmap(12, 12);
		using Graphics graphics = Graphics.FromImage(image);
		graphics.SmoothingMode = SmoothingMode.HighQuality;
		using GraphicsPath graphicsPath = new GraphicsPath();
		graphicsPath.AddEllipse(new Rectangle(0, 0, 11, 11));
		using PathGradientBrush pathGradientBrush = new PathGradientBrush(graphicsPath);
		pathGradientBrush.CenterColor = Color.FromArgb(200, clr);
		pathGradientBrush.SurroundColors = new Color[1] { Color.FromArgb(10, clr) };
		graphics.FillPath(pathGradientBrush, graphicsPath);
		return image;
	}

	private ConnectionStatus DisConnectionHover()
	{
		if (!m_dic_gp_info.ContainsKey(m_gp_hover))
		{
			return ConnectionStatus.DisConnected;
		}
		ConnectionInfo connectionInfo = m_dic_gp_info[m_gp_hover];
		ConnectionStatus connectionStatus = connectionInfo.Output.DisConnectOption(connectionInfo.Input);
		if (connectionStatus == ConnectionStatus.DisConnected)
		{
			m_dic_gp_info.Remove(m_gp_hover);
			m_gp_hover.Dispose();
			m_gp_hover = null;
			Invalidate();
		}
		return connectionStatus;
	}

	private void StartConnect(STNodeOption op)
	{
		if (op.IsInput)
		{
			m_pt_dot_down.X = op.DotLeft;
			m_pt_dot_down.Y = op.DotTop + 5;
		}
		else
		{
			m_pt_dot_down.X = op.DotLeft + op.DotSize;
			m_pt_dot_down.Y = op.DotTop + 5;
		}
		m_ca = CanvasAction.ConnectOption;
		m_option_down = op;
	}

	public void AlignTop()
	{
		if (m_hs_node_selected.Count <= 1)
		{
			return;
		}
		STNode g = m_hs_node_selected.First();
		lock (m_hs_node_selected)
		{
			foreach (STNode item in m_hs_node_selected)
			{
				if (item != g)
				{
					item.Top = g.Top;
				}
			}
		}
	}

	public void AlignLeft()
	{
		if (m_hs_node_selected.Count <= 1)
		{
			return;
		}
		STNode sTNodeHubOption = m_hs_node_selected.First();
		lock (m_hs_node_selected)
		{
			foreach (STNode item in m_hs_node_selected)
			{
				if (item != sTNodeHubOption)
				{
					item.Left = sTNodeHubOption.Left;
				}
			}
		}
	}

	public void AlignVerticalCenter()
	{
		if (m_hs_node_selected.Count <= 1)
		{
			return;
		}
		STNode sTNode = m_hs_node_selected.First();
		int num = sTNode.Left + sTNode.Width / 2;
		lock (m_hs_node_selected)
		{
			foreach (STNode item in m_hs_node_selected)
			{
				if (item != sTNode)
				{
					item.Left = num - item.Width / 2;
				}
			}
		}
	}

	public void AlignHorizontalCenter()
	{
		if (m_hs_node_selected.Count <= 1)
		{
			return;
		}
		STNode sTNodeOption = m_hs_node_selected.First();
		int index = sTNodeOption.Top + sTNodeOption.Height / 2;
		lock (m_hs_node_selected)
		{
			foreach (STNode item in m_hs_node_selected)
			{
				if (item != sTNodeOption)
				{
					item.Top = index - item.Height / 2;
				}
			}
		}
	}

	public void AlignHorizontalDistance()
	{
		if (m_hs_node_selected.Count <= 1)
		{
			return;
		}
		List<STNode> source = m_hs_node_selected.ToList();
		int num = source.Sum((STNode x) => x.Width);
		List<STNode> list = source.OrderBy((STNode p) => p.Left).ToList();
		int num2 = list.Last().Right - list.First().Left;
		int num3 = (num2 - num) / (list.Count - 1);
		if (num3 < 50)
		{
			num3 = 50;
		}
		lock (m_hs_node_selected)
		{
			for (int num4 = 1; num4 < list.Count; num4++)
			{
				STNode sTNode = list[num4];
				STNode sTNode2 = list[num4 - 1];
				sTNode.Left = sTNode2.Left + sTNode2.Width + num3;
			}
		}
	}

	public void AlignVerticalDistance()
	{
		if (m_hs_node_selected.Count <= 1)
		{
			return;
		}
		List<STNode> source = m_hs_node_selected.ToList();
		int num = source.Sum((STNode x) => x.Height);
		List<STNode> list = source.OrderBy((STNode p) => p.Top).ToList();
		int num2 = list.Last().Bottom - list.First().Top;
		int num3 = (num2 - num) / (list.Count - 1);
		if (num3 < 20)
		{
			num3 = 20;
		}
		lock (m_hs_node_selected)
		{
			for (int num4 = 1; num4 < list.Count; num4++)
			{
				STNode sTNode = list[num4];
				STNode sTNode2 = list[num4 - 1];
				sTNode.Top = sTNode2.Top + sTNode2.Height + num3;
			}
		}
	}

	private void MoveNode(Point pt)
	{
		int num = (int)((float)(pt.X - m_pt_down_in_control.X) / _CanvasScale);
		int num2 = (int)((float)(pt.Y - m_pt_down_in_control.Y) / _CanvasScale);
		lock (m_hs_node_selected)
		{
			foreach (STNode item in m_hs_node_selected)
			{
				item.Left = m_dic_pt_selected[item].X + num;
				item.Top = m_dic_pt_selected[item].Y + num2;
			}
			if (_ShowMagnet)
			{
				MagnetInfo magnetInfo = CheckMagnet(_ActiveNode);
				if (magnetInfo.XMatched)
				{
					foreach (STNode item2 in m_hs_node_selected)
					{
						item2.Left -= magnetInfo.OffsetX;
					}
				}
				if (magnetInfo.YMatched)
				{
					foreach (STNode item3 in m_hs_node_selected)
					{
						item3.Top -= magnetInfo.OffsetY;
					}
				}
			}
		}
		Invalidate();
	}

	protected internal virtual void BuildBounds()
	{
		if (_Nodes.Count == 0)
		{
			_CanvasValidBounds = ControlToCanvas(DisplayRectangle);
			return;
		}
		int sTNodeOption = int.MaxValue;
		int typeFromHandle = int.MaxValue;
		int num = int.MinValue;
		int num2 = int.MinValue;
		foreach (STNode node in _Nodes)
		{
			if (sTNodeOption > node.Left)
			{
				sTNodeOption = node.Left;
			}
			if (typeFromHandle > node.Top)
			{
				typeFromHandle = node.Top;
			}
			if (num < node.Right)
			{
				num = node.Right;
			}
			if (num2 < node.Bottom)
			{
				num2 = node.Bottom;
			}
		}
		_CanvasValidBounds.X = sTNodeOption - 60;
		_CanvasValidBounds.Y = typeFromHandle - 60;
		_CanvasValidBounds.Width = num - sTNodeOption + 120;
		_CanvasValidBounds.Height = num2 - typeFromHandle + 120;
	}

	private bool PointInRectangle(Rectangle rect, float x, float y)
	{
		if (x < (float)rect.Left)
		{
			return false;
		}
		if (x > (float)rect.Right)
		{
			return false;
		}
		if (y < (float)rect.Top)
		{
			return false;
		}
		if (y > (float)rect.Bottom)
		{
			return false;
		}
		return true;
	}

	private void BuildMagnetLocation()
	{
		m_lst_magnet_x.Clear();
		m_lst_magnet_y.Clear();
		foreach (STNode node in _Nodes)
		{
			if (!node.IsSelected)
			{
				m_lst_magnet_x.Add(node.Left);
				m_lst_magnet_x.Add(node.Left + node.Width / 2);
				m_lst_magnet_x.Add(node.Left + node.Width);
				m_lst_magnet_y.Add(node.Top);
				m_lst_magnet_y.Add(node.Top + node.Height / 2);
				m_lst_magnet_y.Add(node.Top + node.Height);
			}
		}
	}

	private MagnetInfo CheckMagnet(STNode node)
	{
		m_mi.XMatched = (m_mi.YMatched = false);
		m_lst_magnet_mx.Clear();
		m_lst_magnet_my.Clear();
		m_lst_magnet_mx.Add(node.Left + node.Width / 2);
		m_lst_magnet_mx.Add(node.Left);
		m_lst_magnet_mx.Add(node.Left + node.Width);
		m_lst_magnet_my.Add(node.Top + node.Height / 2);
		m_lst_magnet_my.Add(node.Top);
		m_lst_magnet_my.Add(node.Top + node.Height);
		bool flag = false;
		foreach (int item in m_lst_magnet_mx)
		{
			foreach (int item2 in m_lst_magnet_x)
			{
				if (Math.Abs(item - item2) <= 5)
				{
					flag = true;
					m_mi.X = item2;
					m_mi.OffsetX = item - item2;
					m_mi.XMatched = true;
					break;
				}
			}
			if (flag)
			{
				break;
			}
		}
		flag = false;
		foreach (int item3 in m_lst_magnet_my)
		{
			foreach (int item4 in m_lst_magnet_y)
			{
				if (Math.Abs(item3 - item4) <= 5)
				{
					flag = true;
					m_mi.Y = item4;
					m_mi.OffsetY = item3 - item4;
					m_mi.YMatched = true;
					break;
				}
			}
			if (flag)
			{
				break;
			}
		}
		return m_mi;
	}

	private void DrawBezier(Graphics g, Pen p, PointF ptStart, PointF ptEnd, float f)
	{
		DrawBezier(g, p, ptStart.X, ptStart.Y, ptEnd.X, ptEnd.Y, f);
	}

	private void DrawBezier(Graphics g, Pen p, float x1, float y1, float x2, float y2, float f)
	{
		float num = Math.Abs(x1 - x2) * f;
		if (_Curvature != 0f && num < 30f)
		{
			num = 30f;
		}
		g.DrawBezier(p, x1, y1, x1 + num, y1, x2 - num, y2, x2, y2);
	}

	private GraphicsPath CreateBezierPath(float x1, float y1, float x2, float y2, float f)
	{
		GraphicsPath graphicsPath = new GraphicsPath();
		float num = Math.Abs(x1 - x2) * f;
		if (_Curvature != 0f && num < 30f)
		{
			num = 30f;
		}
		graphicsPath.AddBezier(x1, y1, x1 + num, y1, x2 - num, y2, x2, y2);
		return graphicsPath;
	}

	private void RenderBorder(Graphics g, Rectangle rect, Image img)
	{
		g.DrawImage(img, new Rectangle(rect.X - 5, rect.Y - 5, 5, 5), new Rectangle(0, 0, 5, 5), GraphicsUnit.Pixel);
		g.DrawImage(img, new Rectangle(rect.Right, rect.Y - 5, 5, 5), new Rectangle(img.Width - 5, 0, 5, 5), GraphicsUnit.Pixel);
		g.DrawImage(img, new Rectangle(rect.X - 5, rect.Bottom, 5, 5), new Rectangle(0, img.Height - 5, 5, 5), GraphicsUnit.Pixel);
		g.DrawImage(img, new Rectangle(rect.Right, rect.Bottom, 5, 5), new Rectangle(img.Width - 5, img.Height - 5, 5, 5), GraphicsUnit.Pixel);
		g.DrawImage(img, new Rectangle(rect.X - 5, rect.Y, 5, rect.Height), new Rectangle(0, 5, 5, img.Height - 10), GraphicsUnit.Pixel);
		g.DrawImage(img, new Rectangle(rect.X, rect.Y - 5, rect.Width, 5), new Rectangle(5, 0, img.Width - 10, 5), GraphicsUnit.Pixel);
		g.DrawImage(img, new Rectangle(rect.Right, rect.Y, 5, rect.Height), new Rectangle(img.Width - 5, 5, 5, img.Height - 10), GraphicsUnit.Pixel);
		g.DrawImage(img, new Rectangle(rect.X, rect.Bottom, rect.Width, 5), new Rectangle(5, img.Height - 5, img.Width - 10, 5), GraphicsUnit.Pixel);
	}

	public NodeFindInfo FindNodeFromPoint(PointF pt)
	{
		m_find.Node = null;
		m_find.NodeOption = null;
		m_find.Mark = null;
		for (int num = _Nodes.Count - 1; num >= 0; num--)
		{
			if (!string.IsNullOrEmpty(_Nodes[num].Mark) && PointInRectangle(_Nodes[num].MarkRectangle, pt.X, pt.Y))
			{
				m_find.Mark = _Nodes[num].Mark;
				m_find.MarkLines = _Nodes[num].MarkLines;
				return m_find;
			}
			foreach (STNodeOption inputOption in _Nodes[num].InputOptions)
			{
				if (inputOption != STNodeOption.Empty && PointInRectangle(inputOption.DotRectangle, pt.X, pt.Y))
				{
					m_find.NodeOption = inputOption;
				}
			}
			foreach (STNodeOption outputOption in _Nodes[num].OutputOptions)
			{
				if (outputOption != STNodeOption.Empty && PointInRectangle(outputOption.DotRectangle, pt.X, pt.Y))
				{
					m_find.NodeOption = outputOption;
				}
			}
			if (PointInRectangle(_Nodes[num].Rectangle, pt.X, pt.Y))
			{
				m_find.Node = _Nodes[num];
			}
			if (m_find.NodeOption != null || m_find.Node != null)
			{
				return m_find;
			}
		}
		return m_find;
	}

	public STNode[] GetSelectedNode()
	{
		return m_hs_node_selected.ToArray();
	}

	public float CanvasToControl(float number, bool isX)
	{
		return number * _CanvasScale + (isX ? _CanvasOffsetX : _CanvasOffsetY);
	}

	public PointF CanvasToControl(PointF pt)
	{
		pt.X = pt.X * _CanvasScale + _CanvasOffsetX;
		pt.Y = pt.Y * _CanvasScale + _CanvasOffsetY;
		return pt;
	}

	public Point CanvasToControl(Point pt)
	{
		pt.X = (int)((float)pt.X * _CanvasScale + _CanvasOffsetX);
		pt.Y = (int)((float)pt.Y * _CanvasScale + _CanvasOffsetY);
		return pt;
	}

	public Rectangle CanvasToControl(Rectangle rect)
	{
		rect.X = (int)((float)rect.X * _CanvasScale + _CanvasOffsetX);
		rect.Y = (int)((float)rect.Y * _CanvasScale + _CanvasOffsetY);
		rect.Width = (int)((float)rect.Width * _CanvasScale);
		rect.Height = (int)((float)rect.Height * _CanvasScale);
		return rect;
	}

	public RectangleF CanvasToControl(RectangleF rect)
	{
		rect.X = rect.X * _CanvasScale + _CanvasOffsetX;
		rect.Y = rect.Y * _CanvasScale + _CanvasOffsetY;
		rect.Width *= _CanvasScale;
		rect.Height *= _CanvasScale;
		return rect;
	}

	public float ControlToCanvas(float number, bool isX)
	{
		return (number - (isX ? _CanvasOffsetX : _CanvasOffsetY)) / _CanvasScale;
	}

	public Point ControlToCanvas(Point pt)
	{
		pt.X = (int)(((float)pt.X - _CanvasOffsetX) / _CanvasScale);
		pt.Y = (int)(((float)pt.Y - _CanvasOffsetY) / _CanvasScale);
		return pt;
	}

	public PointF ControlToCanvas(PointF pt)
	{
		pt.X = (pt.X - _CanvasOffsetX) / _CanvasScale;
		pt.Y = (pt.Y - _CanvasOffsetY) / _CanvasScale;
		return pt;
	}

	public Rectangle ControlToCanvas(Rectangle rect)
	{
		rect.X = (int)(((float)rect.X - _CanvasOffsetX) / _CanvasScale);
		rect.Y = (int)(((float)rect.Y - _CanvasOffsetY) / _CanvasScale);
		rect.Width = (int)((float)rect.Width / _CanvasScale);
		rect.Height = (int)((float)rect.Height / _CanvasScale);
		return rect;
	}

	public RectangleF ControlToCanvas(RectangleF rect)
	{
		rect.X = (rect.X - _CanvasOffsetX) / _CanvasScale;
		rect.Y = (rect.Y - _CanvasOffsetY) / _CanvasScale;
		rect.Width /= _CanvasScale;
		rect.Height /= _CanvasScale;
		return rect;
	}

	public void MoveCanvas(float x, float y, bool bAnimation, CanvasMoveArgs ma)
	{
		if (_Nodes.Count == 0)
		{
			m_real_canvas_x = (m_real_canvas_y = 10f);
			return;
		}
		int num = (int)((float)(_CanvasValidBounds.Left + 50) * _CanvasScale);
		int num2 = (int)((float)(_CanvasValidBounds.Top + 50) * _CanvasScale);
		int num3 = (int)((float)(_CanvasValidBounds.Right - 50) * _CanvasScale);
		int num4 = (int)((float)(_CanvasValidBounds.Bottom - 50) * _CanvasScale);
		if ((float)num3 + x < 0f)
		{
			x = -num3;
		}
		if ((float)(base.Width - num) < x)
		{
			x = base.Width - num;
		}
		if ((float)num4 + y < 0f)
		{
			y = -num4;
		}
		if ((float)(base.Height - num2) < y)
		{
			y = base.Height - num2;
		}
		if (bAnimation)
		{
			if ((ma & CanvasMoveArgs.Left) == CanvasMoveArgs.Left)
			{
				m_real_canvas_x = x;
			}
			if ((ma & CanvasMoveArgs.Top) == CanvasMoveArgs.Top)
			{
				m_real_canvas_y = y;
			}
		}
		else
		{
			m_real_canvas_x = (_CanvasOffsetX = x);
			m_real_canvas_y = (_CanvasOffsetY = y);
		}
		OnCanvasMoved(EventArgs.Empty);
	}

	public void ScaleCanvas(float f, float x, float y)
	{
		if (_Nodes.Count == 0)
		{
			_CanvasScale = 1f;
		}
		else if (_CanvasScale != f)
		{
			if ((double)f < 0.2)
			{
				f = 0.2f;
			}
			else if (f > 5f)
			{
				f = 5f;
			}
			float number = ControlToCanvas(x, isX: true);
			float number2 = ControlToCanvas(y, isX: false);
			_CanvasScale = f;
			_CanvasOffsetX = (m_real_canvas_x -= CanvasToControl(number, isX: true) - x);
			_CanvasOffsetY = (m_real_canvas_y -= CanvasToControl(number2, isX: false) - y);
			OnCanvasScaled(EventArgs.Empty);
			Invalidate();
		}
	}

	public ConnectionInfo[] GetConnectionInfo()
	{
		return m_dic_gp_info.Values.ToArray();
	}

	public static bool CanFindNodePath(STNode nodeStart, STNode nodeFind)
	{
		HashSet<STNode> type = new HashSet<STNode>();
		return CanFindNodePath(nodeStart, nodeFind, type);
	}

	private static bool CanFindNodePath(STNode nodeStart, STNode nodeFind, HashSet<STNode> hs)
	{
		foreach (STNodeOption outputOption in nodeStart.OutputOptions)
		{
			if (outputOption.ConnectedOption == null)
			{
				continue;
			}
			foreach (STNodeOption item in outputOption.ConnectedOption)
			{
				if (item.Owner == nodeFind)
				{
					return true;
				}
				if (hs.Add(item.Owner) && CanFindNodePath(item.Owner, nodeFind))
				{
					return true;
				}
			}
		}
		return false;
	}

	public Image GetCanvasImage(Rectangle rect)
	{
		return GetCanvasImage(rect, 1f);
	}

	public Image GetCanvasImage(Rectangle rect, float fScale)
	{
		if ((double)fScale < 0.5)
		{
			fScale = 0.5f;
		}
		else if (fScale > 3f)
		{
			fScale = 3f;
		}
		Image image = new Bitmap((int)((float)rect.Width * fScale), (int)((float)rect.Height * fScale));
		using (Graphics graphics = Graphics.FromImage(image))
		{
			graphics.Clear(BackColor);
			graphics.ScaleTransform(fScale, fScale);
			m_drawing_tools.Graphics = graphics;
			if (_ShowGrid)
			{
				OnDrawGrid(m_drawing_tools, rect.Width, rect.Height);
			}
			graphics.TranslateTransform(-rect.X, -rect.Y);
			OnDrawNode(m_drawing_tools, rect);
			OnDrawConnectedLine(m_drawing_tools);
			graphics.ResetTransform();
			if (_ShowLocation)
			{
				OnDrawNodeOutLocation(m_drawing_tools, image.Size, m_lst_node_out);
			}
		}
		return image;
	}

	public void SaveCanvas(string strFileName)
	{
		using FileStream s = new FileStream(strFileName, FileMode.Create, FileAccess.Write);
		SaveCanvas(s);
	}

	public void SaveCanvas(Stream s)
	{
		Dictionary<STNodeOption, long> dictionary = new Dictionary<STNodeOption, long>();
		s.Write(STNodeConstant.NodeFlag, 0, 4);
		s.WriteByte(1);
		using GZipStream gZipStream = new GZipStream(s, CompressionMode.Compress);
		gZipStream.Write(BitConverter.GetBytes(_CanvasOffsetX), 0, 4);
		gZipStream.Write(BitConverter.GetBytes(_CanvasOffsetY), 0, 4);
		gZipStream.Write(BitConverter.GetBytes(_CanvasScale), 0, 4);
		gZipStream.Write(BitConverter.GetBytes(_Nodes.Count), 0, 4);
		foreach (STNode node in _Nodes)
		{
			try
			{
				byte[] saveData = node.GetSaveData();
				gZipStream.Write(BitConverter.GetBytes(saveData.Length), 0, 4);
				gZipStream.Write(saveData, 0, saveData.Length);
				foreach (STNodeOption inputOption in node.InputOptions)
				{
					if (!dictionary.ContainsKey(inputOption))
					{
						dictionary.Add(inputOption, dictionary.Count);
					}
				}
				foreach (STNodeOption outputOption in node.OutputOptions)
				{
					if (!dictionary.ContainsKey(outputOption))
					{
						dictionary.Add(outputOption, dictionary.Count);
					}
				}
			}
			catch (Exception innerException)
			{
				throw new Exception("获取节点数据出错-" + node.Title, innerException);
			}
		}
		gZipStream.Write(BitConverter.GetBytes(m_dic_gp_info.Count), 0, 4);
		foreach (ConnectionInfo value in m_dic_gp_info.Values)
		{
			gZipStream.Write(BitConverter.GetBytes((dictionary[value.Output] << 32) | dictionary[value.Input]), 0, 8);
		}
	}

	public byte[] GetCanvasData()
	{
		using MemoryStream memoryStream = new MemoryStream();
		SaveCanvas(memoryStream);
		return memoryStream.ToArray();
	}

	public int LoadAssembly(string[] strFiles)
	{
		int num = 0;
		foreach (string strFile in strFiles)
		{
			try
			{
				if (LoadAssembly(strFile))
				{
					num++;
				}
			}
			catch
			{
			}
		}
		return num;
	}

	public bool LoadAssembly(string strFile)
	{
		Assembly asm = Assembly.LoadFrom(strFile);
		return LoadAssembly(asm);
	}

	public bool LoadAssembly(Assembly asm)
	{
		bool result = false;
		if (asm == null)
		{
			return false;
		}
		Type[] types = asm.GetTypes();
		foreach (Type type in types)
		{
			if (!type.IsAbstract && (type == m_type_node || type.IsSubclassOf(m_type_node)) && !m_dic_guid_type.ContainsKey(type.GUID.ToString()))
			{
				m_dic_guid_type.Add(type.GUID.ToString(), type);
				string modelByType = GetModelByType(type);
				if (!m_dic_model_type.ContainsKey(modelByType))
				{
					m_dic_model_type.Add(modelByType, type);
				}
				result = true;
			}
		}
		return result;
	}

	private string GetModelByType(Type t)
	{
		return $"{t.Module.Name}|{t.FullName}";
	}

	public bool LoadAssemblyFromBase64(string base64Assembly)
	{
		byte[] rawAssembly = Convert.FromBase64String(base64Assembly);
		Assembly asm = Assembly.Load(rawAssembly);
		return LoadAssembly(asm);
	}

	public Type[] GetTypes()
	{
		return m_dic_guid_type.Values.ToArray();
	}

	public void LoadCanvas(string strFileName)
	{
		LoadCanvas(File.ReadAllBytes(strFileName));
	}

	public void LoadCanvas(byte[] byData)
	{
		using MemoryStream s = new MemoryStream(byData);
		LoadCanvas(s);
	}

	public void LoadCanvas(Stream s)
	{
		int num = 0;
		byte[] array = new byte[32];
		s.Read(array, 0, 5);
		if (!CheckHeader(array))
		{
			return;
		}
		using (GZipStream gZipStream = new GZipStream(s, CompressionMode.Decompress))
		{
			gZipStream.Read(array, 0, 16);
			float num2 = BitConverter.ToSingle(array, 0);
			float num3 = BitConverter.ToSingle(array, 4);
			float f = BitConverter.ToSingle(array, 8);
			int num4 = BitConverter.ToInt32(array, 12);
			Dictionary<long, STNodeOption> dictionary = new Dictionary<long, STNodeOption>();
			HashSet<STNodeOption> hashSet = new HashSet<STNodeOption>();
			byte[] array2 = null;
			for (int i = 0; i < num4; i++)
			{
				gZipStream.Read(array, 0, 4);
				num = BitConverter.ToInt32(array, 0);
				array2 = new byte[num];
				gZipStream.Read(array2, 0, array2.Length);
				STNode sTNode = null;
				try
				{
					sTNode = GetNodeFromData(array2);
				}
				catch (Exception ex)
				{
					throw new Exception("加载节点时发生错误可能数据已损坏\r\n" + ex.Message, ex);
				}
				if (sTNode == null)
				{
					continue;
				}
				try
				{
					_Nodes.Add(sTNode);
				}
				catch (Exception innerException)
				{
					throw new Exception("加载节点出错-" + sTNode.Title, innerException);
				}
				foreach (STNodeOption inputOption in sTNode.InputOptions)
				{
					if (hashSet.Add(inputOption))
					{
						dictionary.Add(dictionary.Count, inputOption);
					}
				}
				foreach (STNodeOption outputOption in sTNode.OutputOptions)
				{
					if (hashSet.Add(outputOption))
					{
						dictionary.Add(dictionary.Count, outputOption);
					}
				}
			}
			gZipStream.Read(array, 0, 4);
			num4 = BitConverter.ToInt32(array, 0);
			array2 = new byte[8];
			for (int j = 0; j < num4; j++)
			{
				gZipStream.Read(array2, 0, array2.Length);
				long num5 = BitConverter.ToInt64(array2, 0);
				long key = num5 >> 32;
				long key2 = (int)num5;
				if (dictionary.ContainsKey(key) && dictionary.ContainsKey(key2))
				{
					dictionary[key].ConnectOption(dictionary[key2]);
				}
			}
			ScaleCanvas(f, 0f, 0f);
			MoveCanvas(num2, num3, bAnimation: false, CanvasMoveArgs.All);
		}
		BuildBounds();
		foreach (STNode node in _Nodes)
		{
			node.OnEditorLoadCompleted();
		}
	}

	private bool CheckHeader(byte[] header)
	{
		if (BitConverter.ToInt32(header, 0) != STNodeConstant.NodeFlagInt)
		{
			throw new InvalidDataException("无法识别的文件类型");
		}
		if (header[4] != 1)
		{
			throw new InvalidDataException("无法识别的文件版本号");
		}
		return true;
	}

	private STNode GetNodeFromData(byte[] byData)
	{
		int num = 0;
		string text = Encoding.UTF8.GetString(byData, num + 1, byData[num]);
		num += byData[num] + 1;
		string key = Encoding.UTF8.GetString(byData, num + 1, byData[num]);
		num += byData[num] + 1;
		int num2 = 0;
		Dictionary<string, byte[]> dictionary = new Dictionary<string, byte[]>();
		while (num < byData.Length)
		{
			num2 = BitConverter.ToInt32(byData, num);
			num += 4;
			string key2 = Encoding.UTF8.GetString(byData, num, num2);
			num += num2;
			num2 = BitConverter.ToInt32(byData, num);
			num += 4;
			byte[] array = new byte[num2];
			Array.Copy(byData, num, array, 0, num2);
			num += num2;
			dictionary.Add(key2, array);
		}
		Type type = null;
		if (!m_dic_guid_type.ContainsKey(key))
		{
			if (m_dic_model_type.ContainsKey(text))
			{
				type = m_dic_model_type[text];
			}
		}
		else
		{
			type = m_dic_guid_type[key];
		}
		if (type == null)
		{
			throw new TypeLoadException("无法找到类型 {" + text.Split('|')[1] + "} 所在程序集 确保程序集 {" + text.Split('|')[0] + "} 已被编辑器正确加载 可通过调用LoadAssembly()加载程序集");
		}
		STNode sTNode = (STNode)Activator.CreateInstance(type);
		sTNode.Create();
		sTNode.OnLoadNode(dictionary);
		return sTNode;
	}

	public void ShowAlert(string strText, Color foreColor, Color backColor)
	{
		ShowAlert(strText, foreColor, backColor, 1000, AlertLocation.RightBottom, bRedraw: true);
	}

	public void ShowAlert(string strText, Color foreColor, Color backColor, AlertLocation al)
	{
		ShowAlert(strText, foreColor, backColor, 1000, al, bRedraw: true);
	}

	public void ShowAlert(string strText, Color foreColor, Color backColor, int nTime, AlertLocation al, bool bRedraw)
	{
		m_str_alert = strText;
		m_forecolor_alert = foreColor;
		m_backcolor_alert = backColor;
		m_time_alert = nTime;
		m_dt_alert = DateTime.Now;
		m_alpha_alert = 255;
		m_al = al;
		if (bRedraw)
		{
			Invalidate();
		}
	}

	public STNode SetActiveNode(STNode node)
	{
		if (node != null && !_Nodes.Contains(node))
		{
			return _ActiveNode;
		}
		STNode activeNode = _ActiveNode;
		if (_ActiveNode != node)
		{
			if (node != null)
			{
				_Nodes.MoveToEnd(node);
				node.IsActive = true;
				node.SetSelected(bSelected: true, bRedraw: false);
				node.OnGotFocus(EventArgs.Empty);
			}
			if (_ActiveNode != null)
			{
				_ActiveNode.IsActive = false;
				_ActiveNode.OnLostFocus(EventArgs.Empty);
			}
			_ActiveNode = node;
			Invalidate();
			OnActiveChanged(EventArgs.Empty);
		}
		return activeNode;
	}

	public bool AddSelectedNode(STNode node)
	{
		if (!_Nodes.Contains(node))
		{
			return false;
		}
		bool flag = !node.IsSelected;
		node.IsSelected = true;
		lock (m_hs_node_selected)
		{
			return m_hs_node_selected.Add(node) || flag;
		}
	}

	public bool RemoveSelectedNode(STNode node)
	{
		if (!_Nodes.Contains(node))
		{
			return false;
		}
		bool isSelected = node.IsSelected;
		node.IsSelected = false;
		lock (m_hs_node_selected)
		{
			return m_hs_node_selected.Remove(node) || isSelected;
		}
	}

	public Color SetTypeColor(Type t, Color clr)
	{
		return SetTypeColor(t, clr, bReplace: false);
	}

	public Color SetTypeColor(Type t, Color clr, bool bReplace)
	{
		if (_TypeColor.ContainsKey(t))
		{
			if (bReplace)
			{
				_TypeColor[t] = clr;
			}
		}
		else
		{
			_TypeColor.Add(t, clr);
		}
		return _TypeColor[t];
	}
}
