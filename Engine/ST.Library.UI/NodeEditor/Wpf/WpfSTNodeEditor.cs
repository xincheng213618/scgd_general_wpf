using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SD = System.Drawing;
using SWF = System.Windows.Forms;

namespace ST.Library.UI.NodeEditor.Wpf;

public class WpfSTNodeEditor : Control, IDisposable
{
	private const double GridSize = 20d;
	private const int MaxTextCacheEntries = 4096;
	private const int MaxGeometryCacheEntries = 4096;
	private static readonly string[] SummaryPropertyNames = { "NodeName", "DeviceCode", "NodeType" };
	private static readonly Typeface NormalTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
	private static readonly Typeface SemiBoldTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
	private static readonly SolidColorBrush SelectionFillBrush = CreateFrozenBrush(Color.FromArgb(38, 30, 144, 255));
	private static readonly Pen SelectionStrokePen = CreateFrozenPen(Color.FromArgb(180, 30, 144, 255), 1);
	private readonly STNodeEditor _coreEditor;
	private readonly Dictionary<STNode, SD.Point> _dragStartLocations = new Dictionary<STNode, SD.Point>();
	private readonly Dictionary<int, SolidColorBrush> _brushCache = new Dictionary<int, SolidColorBrush>();
	private readonly Dictionary<long, Pen> _penCache = new Dictionary<long, Pen>();
	private readonly Dictionary<Type, PropertyInfo[]> _summaryPropertyCache = new Dictionary<Type, PropertyInfo[]>();
	private readonly Dictionary<TextCacheKey, FormattedText> _textCache = new Dictionary<TextCacheKey, FormattedText>();
	private readonly Dictionary<GeometryCacheKey, StreamGeometry> _geometryCache = new Dictionary<GeometryCacheKey, StreamGeometry>();
	private bool _disposed;
	private bool _isPanning;
	private bool _isDraggingNodes;
	private bool _isSelecting;
	private bool _isConnecting;
	private Point _mouseDownPoint;
	private Point _lastMousePoint;
	private Point _currentMousePoint;
	private Rect _selectionRect;
	private STNodeOption _connectingOption;
	private SD.Point _contextCanvasPoint;
	private STNode _visualActiveNode;
	private bool _hasVisualActiveOverride;
	private int _queuedActiveVersion;
	private SD.Size _lastCoreSize;

	public Action<STNode> ConfigureCreatedNode { get; set; }

	public STNodeEditor CoreEditor => _coreEditor;

	public STNodeCollection Nodes => _coreEditor.Nodes;

	public STNode ActiveNode => _coreEditor.ActiveNode;

	public float CanvasOffsetX => _coreEditor.CanvasOffsetX;

	public float CanvasOffsetY => _coreEditor.CanvasOffsetY;

	public SD.Rectangle CanvasValidBounds => _coreEditor.CanvasValidBounds;

	public float CanvasScale => _coreEditor.CanvasScale;

	public SD.Color BackColor
	{
		get => _coreEditor.BackColor;
		set
		{
			_coreEditor.BackColor = value;
			InvalidateVisual();
		}
	}

	public SD.Color GridColor
	{
		get => _coreEditor.GridColor;
		set
		{
			_coreEditor.GridColor = value;
			InvalidateVisual();
		}
	}

	public SD.Color ForeColor
	{
		get => _coreEditor.ForeColor;
		set
		{
			_coreEditor.ForeColor = value;
			InvalidateVisual();
		}
	}

	public SD.Color LocationBackColor
	{
		get => _coreEditor.LocationBackColor;
		set
		{
			_coreEditor.LocationBackColor = value;
			InvalidateVisual();
		}
	}

	public WpfSTNodeEditor()
	{
		Focusable = true;
		ClipToBounds = true;
		SnapsToDevicePixels = true;
		UseLayoutRounding = true;
		_coreEditor = new STNodeEditor();
		_coreEditor.CreateControl();
		_coreEditor.Invalidated += CoreEditor_Invalidated;
		_coreEditor.NodeAdded += CoreEditor_StructureChanged;
		_coreEditor.NodeRemoved += CoreEditor_StructureChanged;
		_coreEditor.ActiveChanged += CoreEditor_Changed;
		_coreEditor.SelectedChanged += CoreEditor_Changed;
		_coreEditor.OptionConnected += CoreEditor_OptionChanged;
		_coreEditor.OptionDisConnected += CoreEditor_OptionChanged;
		_coreEditor.CanvasMoved += CoreEditor_CanvasChanged;
		_coreEditor.CanvasScaled += CoreEditor_CanvasChanged;

		ContextMenu = new ContextMenu();
		ContextMenu.Opened += ContextMenu_Opened;
		SizeChanged += WpfSTNodeEditor_SizeChanged;
		Unloaded += WpfSTNodeEditor_Unloaded;
		UpdateCoreSize();
	}

	public byte[] GetCanvasData()
	{
		return _coreEditor.GetCanvasData();
	}

	public void LoadCanvas(string fileName)
	{
		ClearRenderCaches(clearText: true);
		_coreEditor.LoadCanvas(fileName);
		InvalidateVisual();
	}

	public void LoadCanvas(byte[] data)
	{
		ClearRenderCaches(clearText: true);
		_coreEditor.LoadCanvas(data);
		InvalidateVisual();
	}

	public void LoadCanvas(Stream stream)
	{
		ClearRenderCaches(clearText: true);
		_coreEditor.LoadCanvas(stream);
		InvalidateVisual();
	}

	public void MoveCanvas(float x, float y, bool bAnimation, CanvasMoveArgs args)
	{
		_coreEditor.MoveCanvasUnbounded(x, y, bAnimation: false, args);
		InvalidateVisual();
	}

	public void ScaleCanvas(float scale, float x, float y)
	{
		_coreEditor.ScaleCanvas(scale, x, y);
		InvalidateVisual();
	}

	public SD.Point ControlToCanvas(SD.Point point)
	{
		return _coreEditor.ControlToCanvas(point);
	}

	public SD.PointF ControlToCanvas(SD.PointF point)
	{
		return _coreEditor.ControlToCanvas(point);
	}

	public SD.Point CanvasToControl(SD.Point point)
	{
		return _coreEditor.CanvasToControl(point);
	}

	public SD.Rectangle CanvasToControl(SD.Rectangle rectangle)
	{
		return _coreEditor.CanvasToControl(rectangle);
	}

	public NodeFindInfo FindNodeFromPoint(SD.PointF point)
	{
		return _coreEditor.FindNodeFromPoint(point);
	}

	public STNode[] GetSelectedNode()
	{
		return _coreEditor.GetSelectedNode();
	}

	public bool AddSelectedNode(STNode node)
	{
		bool result = _coreEditor.AddSelectedNode(node);
		InvalidateVisual();
		return result;
	}

	public bool RemoveSelectedNode(STNode node)
	{
		bool result = _coreEditor.RemoveSelectedNode(node);
		InvalidateVisual();
		return result;
	}

	public STNode SetActiveNode(STNode node)
	{
		STNode result = _coreEditor.SetActiveNode(node);
		InvalidateVisual();
		return result;
	}

	public ConnectionInfo[] GetConnectionInfo()
	{
		return _coreEditor.GetConnectionInfo();
	}

	protected override void OnRender(DrawingContext drawingContext)
	{
		base.OnRender(drawingContext);
		UpdateCoreSize();
		Rect viewRect = new Rect(0, 0, ActualWidth, ActualHeight);
		Rect visibleCanvasRect = GetVisibleCanvasRect(viewRect);
		drawingContext.DrawRectangle(GetBrush(_coreEditor.BackColor), null, viewRect);
		DrawGrid(drawingContext, viewRect);
		DrawConnections(drawingContext, visibleCanvasRect);
		DrawNodes(drawingContext, visibleCanvasRect);
		DrawPendingConnection(drawingContext);
		DrawSelectionRectangle(drawingContext);
	}

	protected override void OnMouseDown(MouseButtonEventArgs e)
	{
		base.OnMouseDown(e);
		if (e.ChangedButton == MouseButton.Middle)
		{
			BeginPan(e.GetPosition(this));
			e.Handled = true;
			return;
		}
	}

	protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
	{
		base.OnMouseLeftButtonDown(e);
		Focus();
		Point viewPoint = e.GetPosition(this);
		_mouseDownPoint = viewPoint;
		_lastMousePoint = viewPoint;
		_currentMousePoint = viewPoint;

		if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
		{
			BeginPan(viewPoint);
			e.Handled = true;
			return;
		}

		NodeFindInfo findInfo = FindNodeFromPoint(ToDrawingPointF(ViewToCanvas(viewPoint)));
		if (findInfo.NodeOption != null)
		{
			_isConnecting = true;
			_connectingOption = findInfo.NodeOption;
			CaptureMouse();
			e.Handled = true;
			InvalidateVisual();
			return;
		}

		if (findInfo.Node != null)
		{
			bool appendSelection = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
			if (!findInfo.Node.IsSelected && !appendSelection)
			{
				ClearSelection();
			}
			ActivateNodeForPointer(findInfo.Node);
			BeginDragNodes(viewPoint);
			e.Handled = true;
			return;
		}

		if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
		{
			ClearSelection();
			QueueActiveNodeUpdate(null);
			_isSelecting = true;
			_selectionRect = new Rect(viewPoint, viewPoint);
			CaptureMouse();
		}
		else
		{
			ClearSelection();
			QueueActiveNodeUpdate(null);
			BeginPan(viewPoint);
		}
		e.Handled = true;
		InvalidateVisual();
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		base.OnMouseMove(e);
		Point viewPoint = e.GetPosition(this);
		_currentMousePoint = viewPoint;

		if (_isPanning)
		{
			Vector delta = viewPoint - _lastMousePoint;
			_coreEditor.MoveCanvasUnbounded(
				_coreEditor.CanvasOffsetX + (float)delta.X,
				_coreEditor.CanvasOffsetY + (float)delta.Y,
				bAnimation: false,
				CanvasMoveArgs.All);
			_lastMousePoint = viewPoint;
			e.Handled = true;
			InvalidateVisual();
			return;
		}

		if (_isDraggingNodes)
		{
			Vector delta = (viewPoint - _mouseDownPoint) / _coreEditor.CanvasScale;
			ClearGeometryCache();
			foreach (KeyValuePair<STNode, SD.Point> item in _dragStartLocations)
			{
				item.Key.Left = item.Value.X + (int)Math.Round(delta.X);
				item.Key.Top = item.Value.Y + (int)Math.Round(delta.Y);
			}
			e.Handled = true;
			InvalidateVisual();
			return;
		}

		if (_isSelecting)
		{
			_selectionRect = new Rect(_mouseDownPoint, viewPoint);
			e.Handled = true;
			InvalidateVisual();
			return;
		}

		if (_isConnecting)
		{
			e.Handled = true;
			InvalidateVisual();
		}
	}

	protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
	{
		base.OnMouseLeftButtonUp(e);
		Point viewPoint = e.GetPosition(this);
		if (_isConnecting)
		{
			CompleteConnection(viewPoint);
			EndMouseAction();
			e.Handled = true;
			return;
		}
		if (_isDraggingNodes)
		{
			EndMouseAction();
			e.Handled = true;
			return;
		}
		if (_isPanning)
		{
			EndMouseAction();
			e.Handled = true;
			return;
		}
		if (_isSelecting)
		{
			CompleteSelection();
			EndMouseAction();
			e.Handled = true;
		}
	}

	protected override void OnMouseUp(MouseButtonEventArgs e)
	{
		base.OnMouseUp(e);
		if (e.ChangedButton == MouseButton.Middle && _isPanning)
		{
			EndMouseAction();
			e.Handled = true;
		}
	}

	protected override void OnMouseWheel(MouseWheelEventArgs e)
	{
		base.OnMouseWheel(e);
		Point point = e.GetPosition(this);
		float delta = e.Delta > 0 ? 0.08f : -0.08f;
		_coreEditor.ScaleCanvas(_coreEditor.CanvasScale + delta, (float)point.X, (float)point.Y);
		e.Handled = true;
		InvalidateVisual();
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		base.OnKeyDown(e);
		if (e.Key == Key.Delete)
		{
			DeleteSelectedNodes();
			e.Handled = true;
			return;
		}
		if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
		{
			foreach (object item in _coreEditor.Nodes)
			{
				if (item is STNode node)
				{
					_coreEditor.AddSelectedNode(node);
				}
			}
			e.Handled = true;
			InvalidateVisual();
		}
	}

	protected override void OnContextMenuOpening(ContextMenuEventArgs e)
	{
		base.OnContextMenuOpening(e);
		Point point = Mouse.GetPosition(this);
		_contextCanvasPoint = ToDrawingPoint(ViewToCanvas(point));
	}

	private void BeginPan(Point viewPoint)
	{
		_isPanning = true;
		_lastMousePoint = viewPoint;
		Cursor = Cursors.SizeAll;
		CaptureMouse();
	}

	private void ActivateNodeForPointer(STNode node)
	{
		if (node == null)
		{
			QueueActiveNodeUpdate(null);
			return;
		}
		_coreEditor.AddSelectedNode(node);
		_visualActiveNode = node;
		InvalidateVisual();
		QueueActiveNodeUpdate(node);
	}

	private void QueueActiveNodeUpdate(STNode node)
	{
		_visualActiveNode = node;
		_hasVisualActiveOverride = true;
		int version = ++_queuedActiveVersion;
		InvalidateVisual();
		Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
		{
			if (_disposed || version != _queuedActiveVersion)
			{
				return;
			}
			_coreEditor.SetActiveNode(node);
			if (_hasVisualActiveOverride && _visualActiveNode == node)
			{
				_visualActiveNode = null;
				_hasVisualActiveOverride = false;
			}
			InvalidateVisual();
		}));
	}

	private void BeginDragNodes(Point viewPoint)
	{
		_dragStartLocations.Clear();
		STNode[] selectedNodes = _coreEditor.GetSelectedNode();
		foreach (STNode node in selectedNodes)
		{
			_dragStartLocations[node] = node.Location;
		}
		_isDraggingNodes = selectedNodes.Length > 0;
		if (_isDraggingNodes)
		{
			Cursor = Cursors.SizeAll;
			CaptureMouse();
		}
	}

	private void EndMouseAction()
	{
		_isPanning = false;
		_isDraggingNodes = false;
		_isSelecting = false;
		_isConnecting = false;
		_connectingOption = null;
		_dragStartLocations.Clear();
		Cursor = Cursors.Arrow;
		if (IsMouseCaptured)
		{
			ReleaseMouseCapture();
		}
		InvalidateVisual();
	}

	private void CompleteConnection(Point viewPoint)
	{
		if (_connectingOption == null)
		{
			return;
		}
		NodeFindInfo findInfo = FindNodeFromPoint(ToDrawingPointF(ViewToCanvas(viewPoint)));
		STNodeOption target = findInfo.NodeOption;
		if (target == null || target == _connectingOption)
		{
			return;
		}
		if (_connectingOption.IsInput && !target.IsInput)
		{
			target.ConnectOption(_connectingOption);
		}
		else
		{
			_connectingOption.ConnectOption(target);
		}
		ClearGeometryCache();
		InvalidateVisual();
	}

	private void CompleteSelection()
	{
		Rect viewRect = Normalize(_selectionRect);
		Rect canvasRect = Normalize(new Rect(ViewToCanvas(viewRect.TopLeft), ViewToCanvas(viewRect.BottomRight)));
		foreach (object item in _coreEditor.Nodes)
		{
			if (item is STNode node && canvasRect.IntersectsWith(ToWpfRect(node.Rectangle)))
			{
				_coreEditor.AddSelectedNode(node);
			}
		}
		InvalidateVisual();
	}

	private void ClearSelection()
	{
		_visualActiveNode = null;
		_hasVisualActiveOverride = true;
		foreach (STNode node in _coreEditor.GetSelectedNode())
		{
			node.SetSelected(false, bRedraw: false);
		}
		InvalidateVisual();
	}

	private void DeleteSelectedNodes()
	{
		STNode[] selected = _coreEditor.GetSelectedNode();
		if (selected.Length == 0 && _coreEditor.ActiveNode != null)
		{
			selected = new[] { _coreEditor.ActiveNode };
		}
		foreach (STNode node in selected.Distinct().ToArray())
		{
			_coreEditor.Nodes.Remove(node);
		}
		ClearRenderCaches(clearText: false);
		InvalidateVisual();
	}

	private void ContextMenu_Opened(object sender, RoutedEventArgs e)
	{
		if (ContextMenu == null)
		{
			return;
		}
		ContextMenu.Items.Clear();
		NodeFindInfo findInfo = FindNodeFromPoint(ToDrawingPointF(new Point(_contextCanvasPoint.X, _contextCanvasPoint.Y)));
		if (findInfo.NodeOption != null && findInfo.NodeOption != STNodeOption.Empty && findInfo.NodeOption.ConnectionCount > 0)
		{
			STNodeOption option = findInfo.NodeOption;
			MenuItem disconnectItem = new MenuItem { Header = "Disconnect" };
			disconnectItem.Click += (s, args) =>
			{
				DisconnectOption(option);
				ClearGeometryCache();
				InvalidateVisual();
			};
			ContextMenu.Items.Add(disconnectItem);
			ContextMenu.Items.Add(new Separator());
		}
		if (findInfo.Node != null)
		{
			MenuItem deleteItem = new MenuItem { Header = Lang.Get("Delete") };
			deleteItem.Click += (s, args) =>
			{
				_coreEditor.Nodes.Remove(findInfo.Node);
				ClearRenderCaches(clearText: false);
				InvalidateVisual();
			};
			ContextMenu.Items.Add(deleteItem);

			MenuItem lockOptionItem = new MenuItem { Header = Lang.Get(nameof(STNode.LockOption)), IsCheckable = true, IsChecked = findInfo.Node.LockOption };
			lockOptionItem.Click += (s, args) =>
			{
				findInfo.Node.LockOption = !findInfo.Node.LockOption;
				InvalidateVisual();
			};
			ContextMenu.Items.Add(lockOptionItem);

			MenuItem lockLocationItem = new MenuItem { Header = Lang.Get(nameof(STNode.LockLocation)), IsCheckable = true, IsChecked = findInfo.Node.LockLocation };
			lockLocationItem.Click += (s, args) =>
			{
				findInfo.Node.LockLocation = !findInfo.Node.LockLocation;
				InvalidateVisual();
			};
			ContextMenu.Items.Add(lockLocationItem);
			ContextMenu.Items.Add(new Separator());
		}

		BuildCreateNodeMenu(ContextMenu.Items);
	}

	private static void DisconnectOption(STNodeOption option)
	{
		if (option.ConnectedOption == null)
		{
			return;
		}
		foreach (STNodeOption connected in option.ConnectedOption.ToArray())
		{
			if (option.IsInput && connected != null)
			{
				connected.DisConnectOption(option);
			}
			else if (connected != null)
			{
				option.DisConnectOption(connected);
			}
		}
	}

	private void BuildCreateNodeMenu(ItemCollection items)
	{
		Type[] nodeTypes = STNodeTypeRegistry.GetTypes()
			.OrderBy(GetNodeMenuPath, StringComparer.CurrentCulture)
			.ThenBy(GetNodeTitle, StringComparer.CurrentCulture)
			.ToArray();

		if (nodeTypes.Length == 0)
		{
			MenuItem empty = new MenuItem { Header = "(No nodes)", IsEnabled = false };
			items.Add(empty);
			return;
		}

		foreach (Type type in nodeTypes)
		{
			string[] segments = GetNodeMenuPath(type)
				.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(LocalizeMenuText)
				.ToArray();
			ItemCollection targetItems = items;
			foreach (string segment in segments)
			{
				targetItems = GetOrCreateMenuItem(targetItems, segment).Items;
			}

			MenuItem nodeItem = new MenuItem { Header = LocalizeMenuText(GetNodeTitle(type)), Tag = type };
			nodeItem.Click += CreateNodeMenuItem_Click;
			targetItems.Add(nodeItem);
		}
	}

	private void CreateNodeMenuItem_Click(object sender, RoutedEventArgs e)
	{
		if (sender is MenuItem menuItem && menuItem.Tag is Type type)
		{
			AddNode(type, _contextCanvasPoint);
		}
	}

	private STNode AddNode(Type type, SD.Point canvasPoint)
	{
		if (!typeof(STNode).IsAssignableFrom(type))
		{
			return null;
		}
		STNode node = (STNode)Activator.CreateInstance(type);
		node.Create();
		node.Left = canvasPoint.X;
		node.Top = canvasPoint.Y;
		ConfigureCreatedNode?.Invoke(node);
		_coreEditor.Nodes.Add(node);
		ActivateNodeForPointer(node);
		ClearRenderCaches(clearText: false);
		InvalidateVisual();
		return node;
	}

	private static MenuItem GetOrCreateMenuItem(ItemCollection items, string header)
	{
		foreach (object item in items)
		{
			if (item is MenuItem menuItem && Equals(menuItem.Header, header))
			{
				return menuItem;
			}
		}
		MenuItem created = new MenuItem { Header = header };
		items.Add(created);
		return created;
	}

	private static string GetNodeMenuPath(Type type)
	{
		STNodeAttribute attribute = type.GetCustomAttribute<STNodeAttribute>();
		if (attribute != null && !string.IsNullOrWhiteSpace(attribute.Path))
		{
			return attribute.Path;
		}
		return "Nodes";
	}

	private static string GetNodeTitle(Type type)
	{
		try
		{
			STNode node = Activator.CreateInstance(type) as STNode;
			if (node != null && !string.IsNullOrWhiteSpace(node.Title))
			{
				return node.Title;
			}
		}
		catch
		{
		}
		return type.Name;
	}

	private static string LocalizeMenuText(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return text;
		}
		string localized = Lang.Get(text);
		if (!string.IsNullOrWhiteSpace(localized) && !string.Equals(localized, $"[{text}]", StringComparison.Ordinal))
		{
			return localized;
		}
		return text;
	}

	private void DrawGrid(DrawingContext dc, Rect viewRect)
	{
		double spacing = GridSize * _coreEditor.CanvasScale;
		if (spacing < 4)
		{
			return;
		}
		SD.Color gridColor = _coreEditor.GridColor;
		Pen majorPen = GetPen(SD.Color.FromArgb(65, gridColor.R, gridColor.G, gridColor.B), 1);
		Pen minorPen = GetPen(SD.Color.FromArgb(30, gridColor.R, gridColor.G, gridColor.B), 1);

		int index = GetGridIndex(_coreEditor.CanvasOffsetX, spacing);
		for (double x = Mod(_coreEditor.CanvasOffsetX, spacing); x < viewRect.Width; x += spacing)
		{
			dc.DrawLine(index++ % 5 == 0 ? majorPen : minorPen, new Point(x, 0), new Point(x, viewRect.Height));
		}
		index = GetGridIndex(_coreEditor.CanvasOffsetY, spacing);
		for (double y = Mod(_coreEditor.CanvasOffsetY, spacing); y < viewRect.Height; y += spacing)
		{
			dc.DrawLine(index++ % 5 == 0 ? majorPen : minorPen, new Point(0, y), new Point(viewRect.Width, y));
		}
	}

	private void DrawConnections(DrawingContext dc, Rect visibleCanvasRect)
	{
		double scale = Math.Max(0.05, _coreEditor.CanvasScale);
		Rect paddedCanvasRect = visibleCanvasRect;
		paddedCanvasRect.Inflate(80 / scale, 80 / scale);
		dc.PushTransform(new MatrixTransform(scale, 0, 0, scale, _coreEditor.CanvasOffsetX, _coreEditor.CanvasOffsetY));
		foreach (object nodeItem in _coreEditor.Nodes)
		{
			if (nodeItem is not STNode node)
			{
				continue;
			}
			foreach (STNodeOption output in node.OutputOptions)
			{
				if (output == null || output == STNodeOption.Empty || output.ConnectedOption == null)
				{
					continue;
				}
				foreach (STNodeOption input in output.ConnectedOption)
				{
					if (input == null || input == STNodeOption.Empty || !input.IsInput)
					{
						continue;
					}
					Point start = GetOutputCanvasPoint(output);
					Point end = GetInputCanvasPoint(input);
					double curvature = GetBezierCurvature(start, end, 30 / scale);
					if (!ConnectionIntersectsCanvas(start, end, paddedCanvasRect, curvature, 8 / scale))
					{
						continue;
					}
					DrawBezier(dc, GetPen(GetOptionColor(output), 2 / scale), start, end, curvature, useCache: true);
				}
			}
		}
		dc.Pop();
	}

	private void DrawPendingConnection(DrawingContext dc)
	{
		if (!_isConnecting || _connectingOption == null)
		{
			return;
		}
		Point start = _connectingOption.IsInput ? GetInputViewPoint(_connectingOption) : GetOutputViewPoint(_connectingOption);
		Point end = _currentMousePoint;
		Pen pen = GetPen(GetOptionColor(_connectingOption), 2, dashed: true);
		DrawBezier(dc, pen, start, end, GetBezierCurvature(start, end, 30), useCache: false);
	}

	private void DrawNodes(DrawingContext dc, Rect visibleCanvasRect)
	{
		foreach (object item in _coreEditor.Nodes)
		{
			if (item is not STNode node || !NodeIntersectsView(node, visibleCanvasRect))
			{
				continue;
			}
			DrawNode(dc, node);
		}
	}

	private void DrawNode(DrawingContext dc, STNode node)
	{
		Rect rect = CanvasToView(node.Rectangle);
		Rect titleRect = CanvasToView(node.TitleRectangle);
		double radius = 5;
		Pen borderPen = GetNodeBorderPen(node);
		dc.DrawRoundedRectangle(GetBrush(node.BackColor), borderPen, rect, radius, radius);
		dc.DrawRoundedRectangle(GetBrush(node.TitleColor), null, titleRect, radius, radius);
		DrawText(dc, node.OnGetDrawTitle(), titleRect, GetBrush(node.ForeColor), 12, TextAlignment.Center, FontWeights.SemiBold);

		DrawNodeSummary(dc, node, rect, titleRect);
		DrawOptions(dc, node);
		if (!string.IsNullOrEmpty(node.Mark))
		{
			DrawMark(dc, node);
		}
	}

	private void DrawNodeSummary(DrawingContext dc, STNode node, Rect nodeRect, Rect titleRect)
	{
		double y = titleRect.Bottom + 4;
		int count = 0;
		foreach (PropertyInfo property in GetSummaryProperties(node.GetType()))
		{
			object rawValue = property.GetValue(node, null);
			if (rawValue == null)
			{
				continue;
			}
			string value = Convert.ToString(rawValue, CultureInfo.CurrentCulture);
			if (string.IsNullOrWhiteSpace(value))
			{
				continue;
			}
			if (y + 14 > nodeRect.Bottom - 2)
			{
				break;
			}
			Rect textRect = new Rect(nodeRect.Left + 18, y, Math.Max(0, nodeRect.Width - 36), 14);
			DrawText(dc, value, textRect, GetBrush(SD.Color.FromArgb(170, node.ForeColor)), 10, TextAlignment.Center, FontWeights.Normal);
			y += 14;
			count++;
			if (count == 2)
			{
				break;
			}
		}
	}

	private void DrawOptions(DrawingContext dc, STNode node)
	{
		foreach (STNodeOption option in node.InputOptions)
		{
			DrawOption(dc, option);
		}
		foreach (STNodeOption option in node.OutputOptions)
		{
			DrawOption(dc, option);
		}
	}

	private void DrawOption(DrawingContext dc, STNodeOption option)
	{
		if (option == null || option == STNodeOption.Empty)
		{
			return;
		}
		Rect dotRect = CanvasToView(option.DotRectangle);
		Brush brush = GetOptionBrush(option);
		Pen pen = option.DataType == typeof(object) ? GetPen(GetOptionColor(option), 1) : null;
		if (option.IsSingle)
		{
			dc.DrawEllipse(option.DataType == typeof(object) ? null : brush, pen, Center(dotRect), dotRect.Width / 2, dotRect.Height / 2);
		}
		else
		{
			dc.DrawRectangle(option.DataType == typeof(object) ? null : brush, pen, dotRect);
		}

		Rect textRect = CanvasToView(option.TextRectangle);
		DrawText(dc, option.Text, textRect, GetBrush(option.TextColor), 11, option.IsInput ? TextAlignment.Left : TextAlignment.Right, FontWeights.Normal);
	}

	private void DrawMark(DrawingContext dc, STNode node)
	{
		Rect rect = CanvasToView(node.MarkRectangle);
		dc.DrawRoundedRectangle(GetBrush(node.MarkColor), null, rect, 3, 3);
		DrawText(dc, node.MarkLines != null && node.MarkLines.Length > 0 ? node.MarkLines[0] : node.Mark, rect, GetBrush(node.ForeColor), 10, TextAlignment.Left, FontWeights.Normal);
	}

	private Pen GetNodeBorderPen(STNode node)
	{
		SD.Color color = _coreEditor.BorderColor;
		double thickness = 1;
		STNode activeNode = _hasVisualActiveOverride ? _visualActiveNode : _coreEditor.ActiveNode;
		if (node == activeNode)
		{
			color = _coreEditor.BorderActiveColor;
			thickness = 2;
		}
		else if (node.IsSelected)
		{
			color = _coreEditor.BorderSelectedColor;
			thickness = 2;
		}
		return GetPen(color, thickness);
	}

	private SolidColorBrush GetOptionBrush(STNodeOption option)
	{
		return GetBrush(GetOptionColor(option));
	}

	private SD.Color GetOptionColor(STNodeOption option)
	{
		if (option.DotColor != SD.Color.Transparent)
		{
			return option.DotColor;
		}
		if (option.DataType != null && option.DataType != typeof(object) && _coreEditor.TypeColor.ContainsKey(option.DataType))
		{
			return _coreEditor.TypeColor[option.DataType];
		}
		return _coreEditor.UnknownTypeColor;
	}

	private void DrawSelectionRectangle(DrawingContext dc)
	{
		if (!_isSelecting)
		{
			return;
		}
		Rect rect = Normalize(_selectionRect);
		dc.DrawRectangle(SelectionFillBrush, SelectionStrokePen, rect);
	}

	private void DrawBezier(DrawingContext dc, Pen pen, Point start, Point end, double curvature, bool useCache)
	{
		StreamGeometry geometry = useCache ? GetConnectionGeometry(start, end, curvature) : CreateBezierGeometry(start, end, curvature);
		dc.DrawGeometry(null, pen, geometry);
	}

	private StreamGeometry GetConnectionGeometry(Point start, Point end, double curvature)
	{
		GeometryCacheKey key = new GeometryCacheKey(start, end, curvature);
		if (_geometryCache.TryGetValue(key, out StreamGeometry geometry))
		{
			return geometry;
		}
		if (_geometryCache.Count >= MaxGeometryCacheEntries)
		{
			_geometryCache.Clear();
		}
		geometry = CreateBezierGeometry(start, end, curvature);
		_geometryCache[key] = geometry;
		return geometry;
	}

	private static StreamGeometry CreateBezierGeometry(Point start, Point end, double curvature)
	{
		StreamGeometry geometry = new StreamGeometry();
		using (StreamGeometryContext context = geometry.Open())
		{
			context.BeginFigure(start, isFilled: false, isClosed: false);
			context.BezierTo(
				new Point(start.X + curvature, start.Y),
				new Point(end.X - curvature, end.Y),
				end,
				isStroked: true,
				isSmoothJoin: true);
		}
		geometry.Freeze();
		return geometry;
	}

	private double GetBezierCurvature(Point start, Point end, double minimum)
	{
		double curvature = Math.Abs(start.X - end.X) * _coreEditor.Curvature;
		if (_coreEditor.Curvature != 0 && curvature < minimum)
		{
			curvature = minimum;
		}
		return curvature;
	}

	private Point GetOutputCanvasPoint(STNodeOption option)
	{
		return new Point(option.DotLeft + option.DotSize, option.DotTop + option.DotSize / 2);
	}

	private Point GetInputCanvasPoint(STNodeOption option)
	{
		return new Point(option.DotLeft - 1, option.DotTop + option.DotSize / 2);
	}

	private Point GetOutputViewPoint(STNodeOption option)
	{
		return CanvasToView(new SD.Point(option.DotLeft + option.DotSize, option.DotTop + option.DotSize / 2));
	}

	private Point GetInputViewPoint(STNodeOption option)
	{
		return CanvasToView(new SD.Point(option.DotLeft - 1, option.DotTop + option.DotSize / 2));
	}

	private Point ViewToCanvas(Point viewPoint)
	{
		return new Point(
			(viewPoint.X - _coreEditor.CanvasOffsetX) / _coreEditor.CanvasScale,
			(viewPoint.Y - _coreEditor.CanvasOffsetY) / _coreEditor.CanvasScale);
	}

	private Point CanvasToView(SD.Point point)
	{
		return new Point(
			point.X * _coreEditor.CanvasScale + _coreEditor.CanvasOffsetX,
			point.Y * _coreEditor.CanvasScale + _coreEditor.CanvasOffsetY);
	}

	private Rect CanvasToView(SD.Rectangle rectangle)
	{
		return new Rect(
			rectangle.X * _coreEditor.CanvasScale + _coreEditor.CanvasOffsetX,
			rectangle.Y * _coreEditor.CanvasScale + _coreEditor.CanvasOffsetY,
			rectangle.Width * _coreEditor.CanvasScale,
			rectangle.Height * _coreEditor.CanvasScale);
	}

	private Rect GetVisibleCanvasRect(Rect viewRect)
	{
		Rect canvasRect = Normalize(new Rect(ViewToCanvas(viewRect.TopLeft), ViewToCanvas(viewRect.BottomRight)));
		double padding = 80 / Math.Max(0.05, _coreEditor.CanvasScale);
		canvasRect.Inflate(padding, padding);
		return canvasRect;
	}

	private static bool NodeIntersectsView(STNode node, Rect visibleCanvasRect)
	{
		Rect nodeRect = ToWpfRect(node.Rectangle);
		if (nodeRect.IntersectsWith(visibleCanvasRect))
		{
			return true;
		}
		if (!string.IsNullOrEmpty(node.Mark))
		{
			Rect markRect = ToWpfRect(node.MarkRectangle);
			return markRect.IntersectsWith(visibleCanvasRect);
		}
		return false;
	}

	private static bool ConnectionIntersectsCanvas(Point start, Point end, Rect canvasRect, double curvature, double padding)
	{
		Rect bounds = BoundsOf(start, end, new Point(start.X + curvature, start.Y), new Point(end.X - curvature, end.Y));
		bounds.Inflate(padding, padding);
		return bounds.IntersectsWith(canvasRect);
	}

	private static Rect ToWpfRect(SD.Rectangle rectangle)
	{
		return new Rect(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
	}

	private static SD.Point ToDrawingPoint(Point point)
	{
		return new SD.Point((int)Math.Round(point.X), (int)Math.Round(point.Y));
	}

	private static SD.PointF ToDrawingPointF(Point point)
	{
		return new SD.PointF((float)point.X, (float)point.Y);
	}

	private static Rect Normalize(Rect rect)
	{
		return new Rect(
			Math.Min(rect.Left, rect.Right),
			Math.Min(rect.Top, rect.Bottom),
			Math.Abs(rect.Width),
			Math.Abs(rect.Height));
	}

	private static Rect BoundsOf(params Point[] points)
	{
		double left = points[0].X;
		double top = points[0].Y;
		double right = points[0].X;
		double bottom = points[0].Y;
		for (int i = 1; i < points.Length; i++)
		{
			Point point = points[i];
			left = Math.Min(left, point.X);
			top = Math.Min(top, point.Y);
			right = Math.Max(right, point.X);
			bottom = Math.Max(bottom, point.Y);
		}
		return new Rect(left, top, right - left, bottom - top);
	}

	private static Point Center(Rect rect)
	{
		return new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);
	}

	private static double Mod(double value, double divisor)
	{
		double result = value % divisor;
		return result < 0 ? result + divisor : result;
	}

	private static int GetGridIndex(double offset, double spacing)
	{
		double value = 5d - offset / spacing;
		if (double.IsNaN(value) || double.IsInfinity(value))
		{
			return 0;
		}
		if (value > int.MaxValue)
		{
			return int.MaxValue;
		}
		if (value < int.MinValue)
		{
			return int.MinValue;
		}
		return (int)value;
	}

	private SolidColorBrush GetBrush(SD.Color color)
	{
		int key = color.ToArgb();
		if (_brushCache.TryGetValue(key, out SolidColorBrush brush))
		{
			return brush;
		}
		brush = new SolidColorBrush(ToMediaColor(color));
		brush.Freeze();
		_brushCache[key] = brush;
		return brush;
	}

	private Pen GetPen(SD.Color color, double thickness, bool dashed = false)
	{
		long key = ((long)color.ToArgb() << 32) ^ ((long)Math.Round(thickness * 100) << 1) ^ (dashed ? 1L : 0L);
		if (_penCache.TryGetValue(key, out Pen pen))
		{
			return pen;
		}
		pen = new Pen(GetBrush(color), thickness);
		if (dashed)
		{
			pen.DashStyle = DashStyles.Dash;
		}
		pen.Freeze();
		_penCache[key] = pen;
		return pen;
	}

	private PropertyInfo[] GetSummaryProperties(Type nodeType)
	{
		if (_summaryPropertyCache.TryGetValue(nodeType, out PropertyInfo[] properties))
		{
			return properties;
		}
		properties = SummaryPropertyNames
			.Select(nodeType.GetProperty)
			.Where(property => property != null && property.CanRead)
			.ToArray();
		_summaryPropertyCache[nodeType] = properties;
		return properties;
	}

	private static Color ToMediaColor(SD.Color color)
	{
		return Color.FromArgb(color.A, color.R, color.G, color.B);
	}

	private static SolidColorBrush CreateFrozenBrush(Color color)
	{
		SolidColorBrush brush = new SolidColorBrush(color);
		brush.Freeze();
		return brush;
	}

	private static Pen CreateFrozenPen(Color color, double thickness)
	{
		Pen pen = new Pen(CreateFrozenBrush(color), thickness);
		pen.Freeze();
		return pen;
	}

	private void DrawText(DrawingContext dc, string text, Rect rect, Brush brush, double fontSize, TextAlignment alignment, FontWeight weight)
	{
		if (string.IsNullOrEmpty(text) || rect.Width <= 0 || rect.Height <= 0)
		{
			return;
		}
		FormattedText formattedText = GetFormattedText(text, rect, brush, fontSize, alignment, weight);
		double y = rect.Top + Math.Max(0, (rect.Height - formattedText.Height) / 2);
		dc.DrawText(formattedText, new Point(rect.Left, y));
	}

	private FormattedText GetFormattedText(string text, Rect rect, Brush brush, double fontSize, TextAlignment alignment, FontWeight weight)
	{
		TextCacheKey key = new TextCacheKey(text, rect.Width, rect.Height, brush.GetHashCode(), fontSize, alignment, weight);
		if (_textCache.TryGetValue(key, out FormattedText formattedText))
		{
			return formattedText;
		}
		if (_textCache.Count >= MaxTextCacheEntries)
		{
			_textCache.Clear();
		}
		formattedText = CreateFormattedText(text, brush, fontSize, alignment, weight);
		formattedText.MaxTextWidth = rect.Width;
		formattedText.MaxTextHeight = rect.Height;
		formattedText.Trimming = TextTrimming.CharacterEllipsis;
		_textCache[key] = formattedText;
		return formattedText;
	}

	private static FormattedText CreateFormattedText(string text, Brush brush, double fontSize, TextAlignment alignment, FontWeight weight)
	{
		Typeface typeface = weight == FontWeights.SemiBold ? SemiBoldTypeface : NormalTypeface;
#if NETFRAMEWORK
		FormattedText formattedText = new FormattedText(
			text,
			CultureInfo.CurrentUICulture,
			FlowDirection.LeftToRight,
			typeface,
			fontSize,
			brush);
#else
		FormattedText formattedText = new FormattedText(
			text,
			CultureInfo.CurrentUICulture,
			FlowDirection.LeftToRight,
			typeface,
			fontSize,
			brush,
			1.0);
#endif
		formattedText.TextAlignment = alignment;
		return formattedText;
	}

	private void WpfSTNodeEditor_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		UpdateCoreSize();
		InvalidateVisual();
	}

	private void UpdateCoreSize()
	{
		int width = Math.Max(100, (int)Math.Round(GetFiniteDimension(ActualWidth, Width)));
		int height = Math.Max(100, (int)Math.Round(GetFiniteDimension(ActualHeight, Height)));
		SD.Size size = new SD.Size(width, height);
		if (size != _lastCoreSize)
		{
			_coreEditor.Size = size;
			_lastCoreSize = size;
		}
	}

	private static double GetFiniteDimension(double primary, double fallback)
	{
		if (!double.IsNaN(primary) && !double.IsInfinity(primary) && primary > 0)
		{
			return primary;
		}
		if (!double.IsNaN(fallback) && !double.IsInfinity(fallback) && fallback > 0)
		{
			return fallback;
		}
		return 100;
	}

	private void CoreEditor_Invalidated(object sender, SWF.InvalidateEventArgs e)
	{
		InvalidateFromCore();
	}

	private void CoreEditor_Changed(object sender, EventArgs e)
	{
		InvalidateFromCore();
	}

	private void CoreEditor_StructureChanged(object sender, EventArgs e)
	{
		InvalidateFromCore(clearGeometry: true, clearText: false);
	}

	private void CoreEditor_CanvasChanged(object sender, EventArgs e)
	{
		InvalidateFromCore();
	}

	private void CoreEditor_OptionChanged(object sender, STNodeEditorOptionEventArgs e)
	{
		InvalidateFromCore(clearGeometry: true, clearText: false);
	}

	private void ClearRenderCaches(bool clearText)
	{
		ClearGeometryCache();
		if (clearText)
		{
			_textCache.Clear();
		}
	}

	private void ClearGeometryCache()
	{
		_geometryCache.Clear();
	}

	private readonly struct TextCacheKey : IEquatable<TextCacheKey>
	{
		private readonly string _text;
		private readonly int _width;
		private readonly int _height;
		private readonly int _brush;
		private readonly int _fontSize;
		private readonly TextAlignment _alignment;
		private readonly int _weight;

		public TextCacheKey(string text, double width, double height, int brush, double fontSize, TextAlignment alignment, FontWeight weight)
		{
			_text = text;
			_width = Quantize(width);
			_height = Quantize(height);
			_brush = brush;
			_fontSize = Quantize(fontSize);
			_alignment = alignment;
			_weight = weight.ToOpenTypeWeight();
		}

		public bool Equals(TextCacheKey other)
		{
			return _width == other._width
				&& _height == other._height
				&& _brush == other._brush
				&& _fontSize == other._fontSize
				&& _alignment == other._alignment
				&& _weight == other._weight
				&& string.Equals(_text, other._text, StringComparison.Ordinal);
		}

		public override bool Equals(object obj)
		{
			return obj is TextCacheKey other && Equals(other);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hash = 17;
				hash = hash * 31 + (_text == null ? 0 : _text.GetHashCode());
				hash = hash * 31 + _width;
				hash = hash * 31 + _height;
				hash = hash * 31 + _brush;
				hash = hash * 31 + _fontSize;
				hash = hash * 31 + (int)_alignment;
				hash = hash * 31 + _weight;
				return hash;
			}
		}
	}

	private readonly struct GeometryCacheKey : IEquatable<GeometryCacheKey>
	{
		private readonly int _startX;
		private readonly int _startY;
		private readonly int _endX;
		private readonly int _endY;
		private readonly int _curvature;

		public GeometryCacheKey(Point start, Point end, double curvature)
		{
			_startX = Quantize(start.X);
			_startY = Quantize(start.Y);
			_endX = Quantize(end.X);
			_endY = Quantize(end.Y);
			_curvature = Quantize(curvature);
		}

		public bool Equals(GeometryCacheKey other)
		{
			return _startX == other._startX
				&& _startY == other._startY
				&& _endX == other._endX
				&& _endY == other._endY
				&& _curvature == other._curvature;
		}

		public override bool Equals(object obj)
		{
			return obj is GeometryCacheKey other && Equals(other);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hash = 17;
				hash = hash * 31 + _startX;
				hash = hash * 31 + _startY;
				hash = hash * 31 + _endX;
				hash = hash * 31 + _endY;
				hash = hash * 31 + _curvature;
				return hash;
			}
		}
	}

	private static int Quantize(double value)
	{
		if (double.IsNaN(value) || double.IsInfinity(value))
		{
			return 0;
		}
		double scaled = Math.Round(value * 10);
		if (scaled > int.MaxValue)
		{
			return int.MaxValue;
		}
		if (scaled < int.MinValue)
		{
			return int.MinValue;
		}
		return (int)scaled;
	}

	private void InvalidateFromCore(bool clearGeometry = false, bool clearText = false)
	{
		if (_disposed)
		{
			return;
		}
		if (Dispatcher.CheckAccess())
		{
			if (clearGeometry)
			{
				ClearRenderCaches(clearText);
			}
			InvalidateVisual();
		}
		else
		{
			Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
			{
				if (_disposed)
				{
					return;
				}
				if (clearGeometry)
				{
					ClearRenderCaches(clearText);
				}
				InvalidateVisual();
			}));
		}
	}

	private void WpfSTNodeEditor_Unloaded(object sender, RoutedEventArgs e)
	{
		if (!IsVisible)
		{
			ReleaseMouseCapture();
		}
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}
		_disposed = true;
		_coreEditor.Invalidated -= CoreEditor_Invalidated;
		_coreEditor.NodeAdded -= CoreEditor_StructureChanged;
		_coreEditor.NodeRemoved -= CoreEditor_StructureChanged;
		_coreEditor.ActiveChanged -= CoreEditor_Changed;
		_coreEditor.SelectedChanged -= CoreEditor_Changed;
		_coreEditor.OptionConnected -= CoreEditor_OptionChanged;
		_coreEditor.OptionDisConnected -= CoreEditor_OptionChanged;
		_coreEditor.CanvasMoved -= CoreEditor_CanvasChanged;
		_coreEditor.CanvasScaled -= CoreEditor_CanvasChanged;
		ContextMenu.Opened -= ContextMenu_Opened;
		SizeChanged -= WpfSTNodeEditor_SizeChanged;
		Unloaded -= WpfSTNodeEditor_Unloaded;
		_coreEditor.Dispose();
		GC.SuppressFinalize(this);
	}
}
