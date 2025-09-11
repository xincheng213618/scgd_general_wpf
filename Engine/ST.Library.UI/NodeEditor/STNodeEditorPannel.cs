using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ST.Library.UI.NodeEditor;

public class STNodeEditorPannel : Control
{
	private bool _LeftLayout = true;

	private Color _SplitLineColor = Color.Black;

	private Color _HandleLineColor = Color.Gray;

	private bool _ShowScale = true;

	private bool _ShowConnectionStatus = true;

	private int _X;

	private int _Y;

	private Point m_pt_down;

	private bool m_is_mx;

	private bool m_is_my;

	private Pen m_pen;

	private bool m_nInited;

	private Dictionary<ConnectionStatus, string> m_dic_status_key = new Dictionary<ConnectionStatus, string>();

	private STNodeEditor m_editor;

	private STNodeTreeView m_tree;

	private STNodePropertyGrid m_grid;

	[Description("获取或设置是否是左边布局")]
	[DefaultValue(true)]
	public bool LeftLayout
	{
		get
		{
			return _LeftLayout;
		}
		set
		{
			if (value != _LeftLayout)
			{
				_LeftLayout = value;
				SetLocation();
				Invalidate();
			}
		}
	}

	[Description("获取或这是分割线颜色")]
	[DefaultValue(typeof(Color), "Black")]
	public Color SplitLineColor
	{
		get
		{
			return _SplitLineColor;
		}
		set
		{
			_SplitLineColor = value;
		}
	}

	[Description("获取或设置分割线手柄颜色")]
	[DefaultValue(typeof(Color), "Gray")]
	public Color HandleLineColor
	{
		get
		{
			return _HandleLineColor;
		}
		set
		{
			_HandleLineColor = value;
		}
	}

	[Description("获取或设置编辑器缩放时候显示比例")]
	[DefaultValue(true)]
	public bool ShowScale
	{
		get
		{
			return _ShowScale;
		}
		set
		{
			_ShowScale = value;
		}
	}

	[Description("获取或设置节点连线时候是否显示状态")]
	[DefaultValue(true)]
	public bool ShowConnectionStatus
	{
		get
		{
			return _ShowConnectionStatus;
		}
		set
		{
			_ShowConnectionStatus = value;
		}
	}

	[Description("获取或设置分割线水平宽度")]
	[DefaultValue(201)]
	public int X
	{
		get
		{
			return _X;
		}
		set
		{
			if (value < 122)
			{
				value = 122;
			}
			else if (value > base.Width - 122)
			{
				value = base.Width - 122;
			}
			if (_X != value)
			{
				_X = value;
				SetLocation();
			}
		}
	}

	[Description("获取或设置分割线垂直高度")]
	public int Y
	{
		get
		{
			return _Y;
		}
		set
		{
			if (value < 122)
			{
				value = 122;
			}
			else if (value > base.Height - 122)
			{
				value = base.Height - 122;
			}
			if (_Y != value)
			{
				_Y = value;
				SetLocation();
			}
		}
	}

	[Description("获取面板中的STNodeEditor")]
	[Browsable(false)]
	public STNodeEditor Editor => m_editor;

	[Description("获取面板中的STNodeTreeView")]
	[Browsable(false)]
	public STNodeTreeView TreeView => m_tree;

	[Description("获取面板中的STNodePropertyGrid")]
	[Browsable(false)]
	public STNodePropertyGrid PropertyGrid => m_grid;

	public override Size MinimumSize
	{
		get
		{
			return base.MinimumSize;
		}
		set
		{
			value = new Size(250, 250);
			base.MinimumSize = value;
		}
	}

	[DllImport("user32.dll")]
	private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int w, int h, bool bRedraw);

	public STNodeEditorPannel()
	{
		SetStyle(ControlStyles.UserPaint, value: true);
		SetStyle(ControlStyles.ResizeRedraw, value: true);
		SetStyle(ControlStyles.AllPaintingInWmPaint, value: true);
		SetStyle(ControlStyles.OptimizedDoubleBuffer, value: true);
		SetStyle(ControlStyles.SupportsTransparentBackColor, value: true);
		m_editor = new STNodeEditor();
		m_tree = new STNodeTreeView();
		m_grid = new STNodePropertyGrid();
		m_grid.Text = "NodeProperty";
		base.Controls.Add(m_editor);
		base.Controls.Add(m_tree);
		base.Controls.Add(m_grid);
		base.Size = new Size(500, 500);
		MinimumSize = new Size(250, 250);
		BackColor = Color.FromArgb(255, 34, 34, 34);
		m_pen = new Pen(BackColor, 3f);
		Type typeFromHandle = typeof(ConnectionStatus);
		Array values = Enum.GetValues(typeFromHandle);
		object value = values.GetValue(0);
		FieldInfo[] fields = typeFromHandle.GetFields();
		foreach (FieldInfo fieldInfo in fields)
		{
			if (!fieldInfo.FieldType.IsEnum)
			{
				continue;
			}
			object[] customAttributes = fieldInfo.GetCustomAttributes(inherit: true);
			foreach (object obj in customAttributes)
			{
				if (obj is DescriptionAttribute)
				{
					m_dic_status_key.Add((ConnectionStatus)fieldInfo.GetValue(fieldInfo), ((DescriptionAttribute)obj).Description);
				}
			}
		}
		m_editor.ActiveChanged += delegate
		{
			m_grid.SetNode(m_editor.ActiveNode);
		};
		m_editor.CanvasScaled += delegate
		{
			if (_ShowScale)
			{
				m_editor.ShowAlert(m_editor.CanvasScale.ToString("F2"), Color.White, Color.FromArgb(127, 255, 255, 0));
			}
		};
		m_editor.OptionConnected += delegate(object s, STNodeEditorOptionEventArgs e)
		{
			if (_ShowConnectionStatus)
			{
				m_editor.ShowAlert(m_dic_status_key[e.Status], Color.White, (e.Status == ConnectionStatus.Connected) ? Color.FromArgb(125, Color.Lime) : Color.FromArgb(125, Color.Red));
			}
		};
	}

	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		if (!m_nInited)
		{
			_Y = base.Height / 2;
			if (_LeftLayout)
			{
				_X = 201;
			}
			else
			{
				_X = base.Width - 202;
			}
			m_nInited = true;
			SetLocation();
		}
		else
		{
			SetLocation();
		}
	}

	protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
	{
		if (width < 250)
		{
			width = 250;
		}
		if (height < 250)
		{
			height = 250;
		}
		base.SetBoundsCore(x, y, width, height, specified);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		Graphics graphics = e.Graphics;
		m_pen.Width = 3f;
		m_pen.Color = _SplitLineColor;
		graphics.DrawLine(m_pen, _X, 0, _X, base.Height);
		int num = 0;
		if (_LeftLayout)
		{
			graphics.DrawLine(m_pen, 0, _Y, _X - 1, _Y);
			num = _X / 2;
		}
		else
		{
			graphics.DrawLine(m_pen, _X + 2, _Y, base.Width, _Y);
			num = _X + (base.Width - _X) / 2;
		}
		m_pen.Width = 1f;
		_HandleLineColor = Color.Gray;
		m_pen.Color = _HandleLineColor;
		graphics.DrawLine(m_pen, _X, _Y - 10, _X, _Y + 10);
		graphics.DrawLine(m_pen, num - 10, _Y, num + 10, _Y);
	}

	private void SetLocation()
	{
		if (_LeftLayout)
		{
			MoveWindow(m_tree.Handle, 0, 0, _X - 1, _Y - 1, bRedraw: false);
			MoveWindow(m_grid.Handle, 0, _Y + 2, _X - 1, base.Height - _Y - 2, bRedraw: false);
			MoveWindow(m_editor.Handle, _X + 2, 0, base.Width - _X - 2, base.Height, bRedraw: false);
		}
		else
		{
			MoveWindow(m_editor.Handle, 0, 0, _X - 1, base.Height, bRedraw: false);
			MoveWindow(m_tree.Handle, _X + 2, 0, base.Width - _X - 2, _Y - 1, bRedraw: false);
			MoveWindow(m_grid.Handle, _X + 2, _Y + 2, base.Width - _X - 2, base.Height - _Y - 2, bRedraw: false);
		}
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		base.OnMouseDown(e);
		m_pt_down = e.Location;
		m_is_mx = (m_is_my = false);
		if (Cursor == Cursors.VSplit)
		{
			m_is_mx = true;
		}
		else if (Cursor == Cursors.HSplit)
		{
			m_is_my = true;
		}
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		if (e.Button == MouseButtons.Left)
		{
			int num = 122;
			int num2 = 122;
			if (m_is_mx)
			{
				_X = e.X;
				if (_X < num)
				{
					_X = num;
				}
				else if (_X + num > base.Width)
				{
					_X = base.Width - num;
				}
			}
			else if (m_is_my)
			{
				_Y = e.Y;
				if (_Y < num2)
				{
					_Y = num2;
				}
				else if (_Y + num2 > base.Height)
				{
					_Y = base.Height - num2;
				}
			}
			SetLocation();
			Invalidate();
		}
		else if (Math.Abs(e.X - _X) < 2)
		{
			Cursor = Cursors.VSplit;
		}
		else if (Math.Abs(e.Y - _Y) < 2)
		{
			Cursor = Cursors.HSplit;
		}
		else
		{
			Cursor = Cursors.Arrow;
		}
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		base.OnMouseLeave(e);
		m_is_mx = (m_is_my = false);
		Cursor = Cursors.Arrow;
	}

	public bool AddSTNode(Type stNodeType)
	{
		return m_tree.AddNode(stNodeType);
	}

	public int LoadAssembly(string strFileName)
	{
		m_editor.LoadAssembly(strFileName);
		return m_tree.LoadAssembly(strFileName);
	}

	public string SetConnectionStatusText(ConnectionStatus status, string strText)
	{
		string text = null;
		if (m_dic_status_key.ContainsKey(status))
		{
			text = m_dic_status_key[status];
			m_dic_status_key[status] = strText;
			return text;
		}
		m_dic_status_key.Add(status, strText);
		return strText;
	}
}
