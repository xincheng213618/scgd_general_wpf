using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace ST.Library.UI.NodeEditor;

public class STNodeTreeView : Control
{
	protected class STNodeTreeCollection : IEnumerable
	{
		private string _Name;

		private SortedDictionary<string, STNodeTreeCollection> m_dic = new SortedDictionary<string, STNodeTreeCollection>();

		public string Name => _Name;

		public string NameLower { get; private set; }

		public Type STNodeType { get; internal set; }

		public STNodeTreeCollection Parent { get; internal set; }

		public int STNodeCount { get; internal set; }

		public string Path { get; internal set; }

		public bool IsOpen { get; set; }

		public bool IsLibraryRoot { get; internal set; }

		public Rectangle DisplayRectangle { get; internal set; }

		public Rectangle SwitchRectangle { get; internal set; }

		public Rectangle InfoRectangle { get; internal set; }

		public Color STNodeTypeColor { get; internal set; }

		public int Count => m_dic.Count;

		public STNodeTreeCollection this[string strKey]
		{
			get
			{
				if (string.IsNullOrEmpty(strKey))
				{
					return null;
				}
				if (m_dic.ContainsKey(strKey))
				{
					return m_dic[strKey];
				}
				return null;
			}
			set
			{
				if (!string.IsNullOrEmpty(strKey) && value != null)
				{
					if (m_dic.ContainsKey(strKey))
					{
						m_dic[strKey] = value;
					}
					else
					{
						m_dic.Add(strKey, value);
					}
					value.Parent = this;
				}
			}
		}

		public STNodeTreeCollection(string strName)
		{
			if (strName == null || strName.Trim() == string.Empty)
			{
				throw new ArgumentNullException("显示名称不能为空");
			}
			_Name = strName.Trim();
			NameLower = _Name.ToLower();
		}

		public STNodeTreeCollection Add(string strName)
		{
			if (!m_dic.ContainsKey(strName))
			{
				m_dic.Add(strName, new STNodeTreeCollection(strName)
				{
					Parent = this
				});
			}
			return m_dic[strName];
		}

		public bool Remove(string strName, bool isAutoDelFolder)
		{
			if (!m_dic.ContainsKey(strName))
			{
				return false;
			}
			bool flag = m_dic.Remove(strName);
			for (STNodeTreeCollection sTNodeTreeCollection = this; sTNodeTreeCollection != null; sTNodeTreeCollection = sTNodeTreeCollection.Parent)
			{
				sTNodeTreeCollection.STNodeCount--;
			}
			if (isAutoDelFolder && m_dic.Count == 0 && Parent != null)
			{
				return flag && Parent.Remove(Name, isAutoDelFolder);
			}
			return flag;
		}

		public void Clear()
		{
			Clear(this);
		}

		private void Clear(STNodeTreeCollection items)
		{
			foreach (STNodeTreeCollection item in items)
			{
				item.Clear(item);
			}
			m_dic.Clear();
		}

		public string[] GetKeys()
		{
			return m_dic.Keys.ToArray();
		}

		public STNodeTreeCollection Copy()
		{
			STNodeTreeCollection sTNodeTreeCollection = new STNodeTreeCollection("COPY");
			Copy(this, sTNodeTreeCollection);
			return sTNodeTreeCollection;
		}

		private void Copy(STNodeTreeCollection items_src, STNodeTreeCollection items_dst)
		{
			foreach (STNodeTreeCollection item in items_src)
			{
				Copy(item, items_dst.Add(item.Name));
			}
			items_dst.Path = items_src.Path;
			items_dst.STNodeType = items_src.STNodeType;
			items_dst.IsLibraryRoot = items_src.IsLibraryRoot;
			items_dst.STNodeCount = items_src.STNodeCount;
			items_dst.STNodeTypeColor = items_src.STNodeTypeColor;
		}

		public IEnumerator GetEnumerator()
		{
			foreach (STNodeTreeCollection value in m_dic.Values)
			{
				yield return value;
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}

	private Color _ItemBackColor = Color.FromArgb(255, 45, 45, 45);

	private Color _ItemHoverColor = Color.FromArgb(50, 125, 125, 125);

	private Color _TitleColor = Color.FromArgb(255, 60, 60, 60);

	private Color _HightLightTextColor = Color.Lime;

	private Color _InfoButtonColor = Color.Gray;

	private Color _FolderCountColor = Color.FromArgb(40, 255, 255, 255);

	private Color _SwitchColor = Color.LightGray;

	private bool _ShowFolderCount = true;

	private bool _ShowInfoButton = true;

	private bool _InfoPanelIsLeftLayout = true;

	private bool _AutoColor = true;

	private STNodeEditor _Editor;

	private STNodePropertyGrid _PropertyGrid;

	private int m_nItemHeight = 29;

	private static Type m_type_node_base = typeof(STNode);

	private static char[] m_chr_splitter = new char[2] { '/', '\\' };

	private STNodeTreeCollection m_items_draw;

	private STNodeTreeCollection m_items_source = new STNodeTreeCollection("ROOT");

	private Dictionary<Type, string> m_dic_all_type = new Dictionary<Type, string>();

	private Pen m_pen;

	private SolidBrush m_brush;

	private StringFormat m_sf;

	private DrawingTools m_dt;

	private Color m_clr_item_1 = Color.FromArgb(10, 0, 0, 0);

	private Color m_clr_item_2 = Color.FromArgb(10, 255, 255, 255);

	private int m_nOffsetY;

	private int m_nSourceOffsetY;

	private int m_nSearchOffsetY;

	private int m_nVHeight;

	private bool m_bHoverInfo;

	private STNodeTreeCollection m_item_hover;

	private Point m_pt_control;

	private Point m_pt_offsety;

	private Rectangle m_rect_clear;

	private string m_str_search;

	private TextBox m_tbx = new TextBox();

	[Description("获取或设置每行属性选项背景色")]
	public Color ItemBackColor
	{
		get
		{
			return _ItemBackColor;
		}
		set
		{
			_ItemBackColor = value;
		}
	}

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

	[Description("获取或设置顶部检索区域背景颜色")]
	public Color TitleColor
	{
		get
		{
			return _TitleColor;
		}
		set
		{
			_TitleColor = value;
			Invalidate(new Rectangle(0, 0, base.Width, m_nItemHeight));
		}
	}

	[Description("获取或设置检索文本框的背景色")]
	public Color TextBoxColor
	{
		get
		{
			return m_tbx.BackColor;
		}
		set
		{
			m_tbx.BackColor = value;
			Invalidate(new Rectangle(0, 0, base.Width, m_nItemHeight));
		}
	}

	[Description("获取或设置检索时候高亮文本颜色")]
	[DefaultValue(typeof(Color), "Lime")]
	public Color HightLightTextColor
	{
		get
		{
			return _HightLightTextColor;
		}
		set
		{
			_HightLightTextColor = value;
		}
	}

	[Description("获取或设置信息显示按钮颜色 若设置AutoColor无法设置此属性值")]
	[DefaultValue(typeof(Color), "Gray")]
	public Color InfoButtonColor
	{
		get
		{
			return _InfoButtonColor;
		}
		set
		{
			_InfoButtonColor = value;
		}
	}

	[Description("获取或设置统计个数的文本颜色")]
	public Color FolderCountColor
	{
		get
		{
			return _FolderCountColor;
		}
		set
		{
			_FolderCountColor = value;
		}
	}

	[Description("获取或设置是否统计STNode的个数")]
	[DefaultValue(typeof(Color), "LightGray")]
	public bool ShowFolderCount
	{
		get
		{
			return _ShowFolderCount;
		}
		set
		{
			_ShowFolderCount = value;
		}
	}

	[Description("获取或设置是否显示信息按钮")]
	[DefaultValue(true)]
	public bool ShowInfoButton
	{
		get
		{
			return _ShowInfoButton;
		}
		set
		{
			_ShowInfoButton = value;
		}
	}

	[Description("获取或设置预览窗口是否是向左布局")]
	[DefaultValue(true)]
	public bool InfoPanelIsLeftLayout
	{
		get
		{
			return _InfoPanelIsLeftLayout;
		}
		set
		{
			_InfoPanelIsLeftLayout = value;
		}
	}

	[Description("获取或设置控件中部分颜色来之对应的STNode的标题颜色")]
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
			Invalidate();
		}
	}

	[Description("获取节点预览时候使用的STNodeEditor")]
	[Browsable(false)]
	public STNodeEditor Editor => _Editor;

	[Description("获取节点预览时候使用的STNodePropertyGrid")]
	[Browsable(false)]
	public STNodePropertyGrid PropertyGrid => _PropertyGrid;

	public STNodeTreeView()
	{
		SetStyle(ControlStyles.UserPaint, value: true);
		SetStyle(ControlStyles.ResizeRedraw, value: true);
		SetStyle(ControlStyles.AllPaintingInWmPaint, value: true);
		SetStyle(ControlStyles.OptimizedDoubleBuffer, value: true);
		SetStyle(ControlStyles.SupportsTransparentBackColor, value: true);
		MinimumSize = new Size(100, 60);
		base.Size = new Size(200, 150);
		m_items_draw = m_items_source;
		m_pen = new Pen(Color.Black);
		m_brush = new SolidBrush(Color.White);
		m_sf = new StringFormat();
		m_sf.LineAlignment = StringAlignment.Center;
		m_dt.Pen = m_pen;
		m_dt.SolidBrush = m_brush;
		ForeColor = Color.FromArgb(255, 220, 220, 220);
		BackColor = Color.FromArgb(255, 35, 35, 35);
		m_tbx.Left = 6;
		m_tbx.BackColor = Color.FromArgb(255, 30, 30, 30);
		m_tbx.ForeColor = ForeColor;
		m_tbx.BorderStyle = BorderStyle.None;
		m_tbx.MaxLength = 20;
		m_tbx.TextChanged += m_tbx_TextChanged;
		base.Controls.Add(m_tbx);
		AllowDrop = true;
		_Editor = new STNodeEditor();
		_PropertyGrid = new STNodePropertyGrid();
	}

	private void m_tbx_TextChanged(object sender, EventArgs e)
	{
		m_str_search = m_tbx.Text.Trim().ToLower();
		m_nSearchOffsetY = 0;
		if (m_str_search == string.Empty)
		{
			m_items_draw = m_items_source;
			Invalidate();
		}
		else
		{
			m_items_draw = m_items_source.Copy();
			Search(m_items_draw, new Stack<string>(), m_str_search);
			Invalidate();
		}
	}

	private bool Search(STNodeTreeCollection items, Stack<string> stack, string strText)
	{
		bool result = false;
		string[] array = new string[items.Count];
		int num = 0;
		foreach (STNodeTreeCollection item in items)
		{
			if (item.NameLower.IndexOf(strText) != -1)
			{
				result = (item.IsOpen = true);
			}
			else if (!Search(item, stack, strText))
			{
				stack.Push(item.Name);
				num++;
			}
			else
			{
				result = (item.IsOpen = true);
			}
		}
		for (int i = 0; i < num; i++)
		{
			items.Remove(stack.Pop(), isAutoDelFolder: false);
		}
		return result;
	}

	private bool AddSTNode(Type stNodeType, STNodeTreeCollection items, string strLibName, bool bShowException)
	{
		if (m_dic_all_type.ContainsKey(stNodeType))
		{
			return false;
		}
		if (stNodeType == null)
		{
			return false;
		}
		if (!stNodeType.IsSubclassOf(m_type_node_base))
		{
			if (bShowException)
			{
				throw new ArgumentException("不支持的类型[" + stNodeType.FullName + "] [stNodeType]参数值必须为[STNode]子类的类型");
			}
			return false;
		}
		STNodeAttribute nodeAttribute = GetNodeAttribute(stNodeType);
		if (nodeAttribute == null)
		{
			if (bShowException)
			{
				throw new InvalidOperationException("类型[" + stNodeType.FullName + "]未被[STNodeAttribute]所标记");
			}
			return false;
		}
		string text = string.Empty;
		items.STNodeCount++;
		if (!string.IsNullOrEmpty(nodeAttribute.Path))
		{
			string[] array = nodeAttribute.Path.Split(m_chr_splitter);
			for (int i = 0; i < array.Length; i++)
			{
				items = items.Add(array[i]);
				items.STNodeCount++;
				text = text + "/" + array[i];
			}
		}
		try
		{
			STNode sTNode = (STNode)Activator.CreateInstance(stNodeType);
			STNodeTreeCollection sTNodeTreeCollection = new STNodeTreeCollection(sTNode.Title);
			sTNodeTreeCollection.Path = (strLibName + "/" + nodeAttribute.Path).Trim('/');
			sTNodeTreeCollection.STNodeType = stNodeType;
			items[sTNodeTreeCollection.Name] = sTNodeTreeCollection;
			sTNodeTreeCollection.STNodeTypeColor = sTNode.TitleColor;
			m_dic_all_type.Add(stNodeType, sTNodeTreeCollection.Path);
			Invalidate();
		}
		catch (Exception ex)
		{
			if (bShowException)
			{
				throw ex;
			}
			return false;
		}
		return true;
	}

	private STNodeTreeCollection AddAssemblyPrivate(string strFile)
	{
		strFile = Path.GetFullPath(strFile);
		Assembly assembly = Assembly.LoadFrom(strFile);
		STNodeTreeCollection sTNodeTreeCollection = new STNodeTreeCollection(Path.GetFileNameWithoutExtension(strFile));
		Type[] types = assembly.GetTypes();
		foreach (Type type in types)
		{
			if (!type.IsAbstract && type.IsSubclassOf(m_type_node_base))
			{
				AddSTNode(type, sTNodeTreeCollection, sTNodeTreeCollection.Name, bShowException: false);
			}
		}
		return sTNodeTreeCollection;
	}

	private STNodeAttribute GetNodeAttribute(Type stNodeType)
	{
		if (stNodeType == null)
		{
			return null;
		}
		object[] customAttributes = stNodeType.GetCustomAttributes(inherit: true);
		foreach (object obj in customAttributes)
		{
			if (obj is STNodeAttribute)
			{
				return (STNodeAttribute)obj;
			}
		}
		return null;
	}

	private STNodeTreeCollection FindItemByPoint(STNodeTreeCollection items, Point pt)
	{
		foreach (STNodeTreeCollection item in items)
		{
			if (item.DisplayRectangle.Contains(pt))
			{
				return item;
			}
			if (item.IsOpen)
			{
				STNodeTreeCollection sTNodeTreeCollection2 = FindItemByPoint(item, pt);
				if (sTNodeTreeCollection2 != null)
				{
					return sTNodeTreeCollection2;
				}
			}
		}
		return null;
	}

	protected override void OnCreateControl()
	{
		base.OnCreateControl();
		m_tbx.Top = (m_nItemHeight - m_tbx.Height) / 2;
	}

	protected override void OnResize(EventArgs e)
	{
		base.OnResize(e);
		m_tbx.Width = base.Width - 29;
		m_rect_clear = new Rectangle(base.Width - 20, 9, 12, 12);
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		base.OnPaint(e);
		m_nOffsetY = (string.IsNullOrEmpty(m_str_search) ? m_nSourceOffsetY : m_nSearchOffsetY);
		Graphics graphics = e.Graphics;
		m_dt.Graphics = graphics;
		graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
		graphics.TranslateTransform(0f, m_nOffsetY);
		int num = 0;
		foreach (STNodeTreeCollection item in m_items_draw)
		{
			num = OnStartDrawItem(m_dt, item, num, 0);
		}
		m_nVHeight = (num + 1) * m_nItemHeight;
		foreach (STNodeTreeCollection item2 in m_items_draw)
		{
			OnDrawSwitch(m_dt, item2);
		}
		graphics.ResetTransform();
		OnDrawSearch(m_dt);
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		bool flag = false;
		m_pt_offsety = (m_pt_control = e.Location);
		m_pt_offsety.Y -= m_nOffsetY;
		if (!string.IsNullOrEmpty(m_str_search) && m_rect_clear.Contains(e.Location))
		{
			Cursor = Cursors.Hand;
		}
		else
		{
			Cursor = Cursors.Arrow;
		}
		STNodeTreeCollection sTNodeTreeCollection = FindItemByPoint(m_items_draw, m_pt_offsety);
		if (m_item_hover != sTNodeTreeCollection)
		{
			m_item_hover = sTNodeTreeCollection;
			flag = true;
		}
		if (sTNodeTreeCollection != null)
		{
			bool flag2 = sTNodeTreeCollection.InfoRectangle.Contains(m_pt_offsety);
			if (flag2 != m_bHoverInfo)
			{
				m_bHoverInfo = flag2;
				flag = true;
			}
		}
		if (flag)
		{
			Invalidate();
		}
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		base.OnMouseDown(e);
		Focus();
		if (!string.IsNullOrEmpty(m_str_search) && m_rect_clear.Contains(e.Location))
		{
			m_tbx.Text = string.Empty;
			return;
		}
		m_pt_offsety = (m_pt_control = e.Location);
		m_pt_offsety.Y -= m_nOffsetY;
		if (m_item_hover != null)
		{
			if (m_item_hover.SwitchRectangle.Contains(m_pt_offsety))
			{
				m_item_hover.IsOpen = !m_item_hover.IsOpen;
				Invalidate();
			}
			else if (m_item_hover.InfoRectangle.Contains(m_pt_offsety))
			{
				Rectangle rectangle = RectangleToScreen(m_item_hover.DisplayRectangle);
				FrmNodePreviewPanel frmNodePreviewPanel = new FrmNodePreviewPanel(m_item_hover.STNodeType, new Point(rectangle.Right - m_nItemHeight, rectangle.Top + m_nOffsetY), m_nItemHeight, _InfoPanelIsLeftLayout, _Editor, _PropertyGrid);
				frmNodePreviewPanel.BackColor = BackColor;
				frmNodePreviewPanel.Show(this);
			}
			else if (m_item_hover.STNodeType != null)
			{
				DataObject data = new DataObject("STNodeType", m_item_hover.STNodeType);
				DoDragDrop(data, DragDropEffects.Copy);
			}
		}
	}

	protected override void OnMouseDoubleClick(MouseEventArgs e)
	{
		base.OnMouseDoubleClick(e);
		m_pt_offsety = (m_pt_control = e.Location);
		m_pt_offsety.Y -= m_nOffsetY;
		STNodeTreeCollection sTNodeTreeCollection = FindItemByPoint(m_items_draw, m_pt_offsety);
		if (sTNodeTreeCollection != null && !(sTNodeTreeCollection.STNodeType != null))
		{
			sTNodeTreeCollection.IsOpen = !sTNodeTreeCollection.IsOpen;
			Invalidate();
		}
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		base.OnMouseLeave(e);
		if (m_item_hover != null)
		{
			m_item_hover = null;
			Invalidate();
		}
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
			m_nOffsetY += m_nItemHeight;
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
			m_nOffsetY -= m_nItemHeight;
		}
		if (string.IsNullOrEmpty(m_str_search))
		{
			m_nSourceOffsetY = m_nOffsetY;
		}
		else
		{
			m_nSearchOffsetY = m_nOffsetY;
		}
		Invalidate();
	}

	protected virtual void OnDrawSearch(DrawingTools dt)
	{
		Graphics graphics = dt.Graphics;
		m_brush.Color = _TitleColor;
		graphics.FillRectangle(m_brush, 0, 0, base.Width, m_nItemHeight);
		m_brush.Color = m_tbx.BackColor;
		graphics.FillRectangle(m_brush, 5, 5, base.Width - 10, m_nItemHeight - 10);
		m_pen.Color = ForeColor;
		if (string.IsNullOrEmpty(m_str_search))
		{
			graphics.SmoothingMode = SmoothingMode.HighQuality;
			graphics.DrawEllipse(m_pen, base.Width - 17, 8, 8, 8);
			graphics.SmoothingMode = SmoothingMode.None;
			graphics.DrawLine(m_pen, base.Width - 13, 17, base.Width - 13, m_nItemHeight - 9);
		}
		else
		{
			m_pen.Color = ForeColor;
			graphics.SmoothingMode = SmoothingMode.HighQuality;
			graphics.DrawEllipse(m_pen, base.Width - 20, 9, 10, 10);
			graphics.DrawLine(m_pen, base.Width - 18, 11, base.Width - 12, 17);
			graphics.DrawLine(m_pen, base.Width - 12, 11, base.Width - 18, 17);
			graphics.SmoothingMode = SmoothingMode.None;
		}
	}

	protected virtual int OnStartDrawItem(DrawingTools dt, STNodeTreeCollection Items, int nCounter, int nLevel)
	{
		Graphics graphics = dt.Graphics;
		Items.DisplayRectangle = new Rectangle(0, m_nItemHeight * (nCounter + 1), base.Width, m_nItemHeight);
		Items.SwitchRectangle = new Rectangle(5 + nLevel * 10, (nCounter + 1) * m_nItemHeight, 10, m_nItemHeight);
		if (_ShowInfoButton && Items.STNodeType != null)
		{
			Items.InfoRectangle = new Rectangle(base.Width - 18, Items.DisplayRectangle.Top + (m_nItemHeight - 14) / 2, 14, 14);
		}
		else
		{
			Items.InfoRectangle = Rectangle.Empty;
		}
		OnDrawItem(dt, Items, nCounter++, nLevel);
		if (!Items.IsOpen)
		{
			return nCounter;
		}
		foreach (STNodeTreeCollection Item in Items)
		{
			if (Item.STNodeType == null)
			{
				nCounter = OnStartDrawItem(dt, Item, nCounter++, nLevel + 1);
			}
		}
		foreach (STNodeTreeCollection Item2 in Items)
		{
			if (Item2.STNodeType != null)
			{
				nCounter = OnStartDrawItem(dt, Item2, nCounter++, nLevel + 1);
			}
		}
		foreach (STNodeTreeCollection Item3 in Items)
		{
			OnDrawSwitch(dt, Item3);
		}
		return nCounter;
	}

	protected virtual void OnDrawItem(DrawingTools dt, STNodeTreeCollection items, int nCounter, int nLevel)
	{
		Graphics graphics = dt.Graphics;
		m_brush.Color = ((nCounter % 2 == 0) ? m_clr_item_1 : m_clr_item_2);
		graphics.FillRectangle(m_brush, items.DisplayRectangle);
		if (items == m_item_hover)
		{
			m_brush.Color = _ItemHoverColor;
			graphics.FillRectangle(m_brush, items.DisplayRectangle);
		}
		Rectangle rect = new Rectangle(45 + nLevel * 10, items.SwitchRectangle.Top, base.Width - 45 - nLevel * 10, m_nItemHeight);
		m_pen.Color = Color.FromArgb(100, 125, 125, 125);
		graphics.DrawLine(m_pen, 9, items.SwitchRectangle.Top + m_nItemHeight / 2, items.SwitchRectangle.Left + 19, items.SwitchRectangle.Top + m_nItemHeight / 2);
		if (nCounter != 0)
		{
			for (int i = 0; i <= nLevel; i++)
			{
				graphics.DrawLine(m_pen, 9 + i * 10, items.SwitchRectangle.Top - m_nItemHeight / 2, 9 + i * 10, items.SwitchRectangle.Top + m_nItemHeight / 2 - 1);
			}
		}
		OnDrawItemText(dt, items, rect);
		OnDrawItemIcon(dt, items, rect);
	}

	protected virtual void OnDrawSwitch(DrawingTools dt, STNodeTreeCollection items)
	{
		Graphics graphics = dt.Graphics;
		if (items.Count != 0)
		{
			m_pen.Color = _SwitchColor;
			m_brush.Color = m_pen.Color;
			int num = items.SwitchRectangle.Y + m_nItemHeight / 2 - 4;
			graphics.DrawRectangle(m_pen, items.SwitchRectangle.Left, num, 8, 8);
			graphics.DrawLine(m_pen, items.SwitchRectangle.Left + 1, num + 4, items.SwitchRectangle.Right - 3, num + 4);
			if (!items.IsOpen)
			{
				graphics.DrawLine(m_pen, items.SwitchRectangle.Left + 4, num + 1, items.SwitchRectangle.Left + 4, num + 7);
			}
		}
	}

	protected virtual void OnDrawItemText(DrawingTools dt, STNodeTreeCollection items, Rectangle rect)
	{
		Graphics graphics = dt.Graphics;
		rect.Width -= 20;
		m_sf.FormatFlags = StringFormatFlags.NoWrap;
		if (!string.IsNullOrEmpty(m_str_search))
		{
			int num = items.NameLower.IndexOf(m_str_search);
			if (num != -1)
			{
				CharacterRange[] measurableCharacterRanges = new CharacterRange[1]
				{
					new CharacterRange(num, m_str_search.Length)
				};
				m_sf.SetMeasurableCharacterRanges(measurableCharacterRanges);
				Region[] array = graphics.MeasureCharacterRanges(items.Name, Font, rect, m_sf);
				graphics.SetClip(array[0], CombineMode.Intersect);
				m_brush.Color = _HightLightTextColor;
				graphics.DrawString(items.Name, Font, m_brush, rect, m_sf);
				graphics.ResetClip();
				graphics.SetClip(array[0], CombineMode.Exclude);
				m_brush.Color = ((items.STNodeType == null) ? Color.FromArgb(ForeColor.A / 2, ForeColor) : ForeColor);
				graphics.DrawString(items.Name, Font, m_brush, rect, m_sf);
				graphics.ResetClip();
				return;
			}
		}
		m_brush.Color = ((items.STNodeType == null) ? Color.FromArgb(ForeColor.A * 2 / 3, ForeColor) : ForeColor);
		graphics.DrawString(items.Name, Font, m_brush, rect, m_sf);
	}

	protected virtual void OnDrawItemIcon(DrawingTools dt, STNodeTreeCollection items, Rectangle rect)
	{
		Graphics graphics = dt.Graphics;
		if (items.STNodeType != null)
		{
			m_pen.Color = (_AutoColor ? items.STNodeTypeColor : Color.DarkCyan);
			m_brush.Color = Color.LightGray;
			graphics.DrawRectangle(m_pen, rect.Left - 15, rect.Top + m_nItemHeight / 2 - 5, 11, 10);
			graphics.FillRectangle(m_brush, rect.Left - 17, rect.Top + m_nItemHeight / 2 - 2, 5, 5);
			graphics.FillRectangle(m_brush, rect.Left - 6, rect.Top + m_nItemHeight / 2 - 2, 5, 5);
			if (m_item_hover == items && m_bHoverInfo)
			{
				m_brush.Color = BackColor;
				graphics.FillRectangle(m_brush, items.InfoRectangle);
			}
			m_pen.Color = (_AutoColor ? items.STNodeTypeColor : _InfoButtonColor);
			m_pen.Width = 2f;
			graphics.DrawLine(m_pen, items.InfoRectangle.X + 4, items.InfoRectangle.Y + 3, items.InfoRectangle.X + 10, items.InfoRectangle.Y + 3);
			graphics.DrawLine(m_pen, items.InfoRectangle.X + 4, items.InfoRectangle.Y + 6, items.InfoRectangle.X + 10, items.InfoRectangle.Y + 6);
			graphics.DrawLine(m_pen, items.InfoRectangle.X + 4, items.InfoRectangle.Y + 11, items.InfoRectangle.X + 10, items.InfoRectangle.Y + 11);
			graphics.DrawLine(m_pen, items.InfoRectangle.X + 7, items.InfoRectangle.Y + 7, items.InfoRectangle.X + 7, items.InfoRectangle.Y + 10);
			m_pen.Width = 1f;
			graphics.DrawRectangle(m_pen, items.InfoRectangle.X, items.InfoRectangle.Y, items.InfoRectangle.Width - 1, items.InfoRectangle.Height - 1);
		}
		else
		{
			if (items.IsLibraryRoot)
			{
				Rectangle rect2 = new Rectangle(rect.Left - 15, rect.Top + m_nItemHeight / 2 - 5, 11, 10);
				graphics.DrawRectangle(Pens.Gray, rect2);
				graphics.DrawLine(Pens.Cyan, rect2.X - 2, rect2.Top, rect2.X + 2, rect2.Top);
				graphics.DrawLine(Pens.Cyan, rect2.X, rect2.Y - 2, rect2.X, rect2.Y + 2);
				graphics.DrawLine(Pens.Cyan, rect2.Right - 2, rect2.Bottom, rect2.Right + 2, rect2.Bottom);
				graphics.DrawLine(Pens.Cyan, rect2.Right, rect2.Bottom - 2, rect2.Right, rect2.Bottom + 2);
			}
			else
			{
				graphics.DrawRectangle(Pens.Goldenrod, new Rectangle(rect.Left - 16, rect.Top + m_nItemHeight / 2 - 6, 8, 3));
				graphics.DrawRectangle(Pens.Goldenrod, new Rectangle(rect.Left - 16, rect.Top + m_nItemHeight / 2 - 3, 13, 9));
			}
			if (_ShowFolderCount)
			{
				m_sf.Alignment = StringAlignment.Far;
				m_brush.Color = _FolderCountColor;
				rect.X -= 4;
				graphics.DrawString("[" + items.STNodeCount + "]", Font, m_brush, rect, m_sf);
				m_sf.Alignment = StringAlignment.Near;
			}
		}
	}

	public void Search(string strText)
	{
		if (strText != null && !(strText.Trim() == string.Empty))
		{
			m_tbx.Text = strText.Trim();
		}
	}

	public bool AddNode(Type stNodeType)
	{
		return AddSTNode(stNodeType, m_items_source, null, bShowException: true);
	}

	public int LoadAssembly(string strFile)
	{
		strFile = Path.GetFullPath(strFile);
		STNodeTreeCollection sTNodeTreeCollection = AddAssemblyPrivate(strFile);
		if (sTNodeTreeCollection.STNodeCount == 0)
		{
			return 0;
		}
		sTNodeTreeCollection.IsLibraryRoot = true;
		m_items_source[sTNodeTreeCollection.Name] = sTNodeTreeCollection;
		return sTNodeTreeCollection.STNodeCount;
	}

	public void Clear()
	{
		m_items_source.Clear();
		m_items_draw.Clear();
		m_dic_all_type.Clear();
		Invalidate();
	}

	public bool RemoveNode(Type stNodeType)
	{
		if (!m_dic_all_type.ContainsKey(stNodeType))
		{
			return false;
		}
		string text = m_dic_all_type[stNodeType];
		STNodeTreeCollection sTNodeTreeCollection = m_items_source;
		if (!string.IsNullOrEmpty(text))
		{
			string[] array = text.Split(m_chr_splitter);
			for (int i = 0; i < array.Length; i++)
			{
				sTNodeTreeCollection = sTNodeTreeCollection[array[i]];
				if (sTNodeTreeCollection == null)
				{
					return false;
				}
			}
		}
		try
		{
			STNode sTNode = (STNode)Activator.CreateInstance(stNodeType);
			if (sTNodeTreeCollection[sTNode.Title] == null)
			{
				return false;
			}
			sTNodeTreeCollection.Remove(sTNode.Title, isAutoDelFolder: true);
			m_dic_all_type.Remove(stNodeType);
		}
		catch
		{
			return false;
		}
		Invalidate();
		return true;
	}
}
