using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using FlowEngineLib.Base;
using FlowEngineLib.Start;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

public class FlowEngineControl : FlowEngineAPI, IDisposable
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(FlowEngineControl));

	protected STNodeEditor NodeEditor;

	protected Dictionary<string, BaseStartNode> startNodeNames;

	protected Dictionary<string, ServiceNode> services;

	protected Dictionary<string, byte[]> loadedCanvas;

	protected bool IsAutoStartName;

	protected bool _IsRunning;

	protected readonly FlowNodeManager NodeManager;

	private readonly List<BaseStartNode> attachedStartNodes;

	private readonly Dictionary<CVBaseServerNode, DeviceNode> attachedDeviceNodes;

	private readonly HashSet<STNode> attachedNodes;

	private readonly object lifecycleLock = new object();

	private readonly object stateLock = new object();

	private bool isDisposed;

	public bool IsReady => GetFlowReady();

	public bool IsRunning
	{
		get
		{
			lock (stateLock)
			{
				return _IsRunning;
			}
		}
	}

	public event FlowEngineEventHandler Finished;

	private bool GetFlowReady()
	{
		lock (stateLock)
		{
			if (startNodeNames.Count > 0)
			{
				return startNodeNames.First().Value.Ready;
			}
			return false;
		}
	}

	public FlowEngineControl(STNodeEditor nodeEditor, bool isAutoStartName)
		: this(nodeEditor, isAutoStartName, FlowNodeManager.Instance)
	{
	}

	public FlowEngineControl(STNodeEditor nodeEditor, bool isAutoStartName, FlowNodeManager nodeManager)
		: this(isAutoStartName, nodeManager)
	{
		AttachNodeEditor(nodeEditor);
	}

	public FlowEngineControl(bool isAutoStartName)
		: this(isAutoStartName, FlowNodeManager.Instance)
	{
	}

	public FlowEngineControl(bool isAutoStartName, FlowNodeManager nodeManager)
	{
		NodeManager = nodeManager ?? throw new ArgumentNullException(nameof(nodeManager));
		startNodeNames = new Dictionary<string, BaseStartNode>();
		IsAutoStartName = isAutoStartName;
		services = new Dictionary<string, ServiceNode>();
		loadedCanvas = new Dictionary<string, byte[]>();
		attachedStartNodes = new List<BaseStartNode>();
		attachedDeviceNodes = new Dictionary<CVBaseServerNode, DeviceNode>();
		attachedNodes = new HashSet<STNode>();
		_IsRunning = false;
	}

	public FlowEngineControl AttachNodeEditor(STNodeEditor nodeEditor)
	{
		if (nodeEditor == null)
		{
			throw new ArgumentNullException(nameof(nodeEditor));
		}
		lock (lifecycleLock)
		{
			lock (stateLock)
			{
				ThrowIfDisposedLocked();
				if (ReferenceEquals(NodeEditor, nodeEditor))
				{
					return this;
				}
			}
			DetachNodeEditorCore();
			lock (stateLock)
			{
				ThrowIfDisposedLocked();
				NodeEditor = nodeEditor;
				NodeEditor.NodeAdded += NodeEditor_NodeAdded;
				NodeEditor.NodeRemoved += NodeEditor_NodeRemoved;
				NodeEditor.OptionConnected += NodeEditor_OptionChanged;
				NodeEditor.OptionDisConnected += NodeEditor_OptionChanged;
				NodeEditor.NodeLocationChanged += NodeEditor_NodeLocationChanged;
				NodeEditor.HistoryChanged += NodeEditor_HistoryChanged;
			}
			foreach (STNode node in nodeEditor.Nodes)
			{
				RegisterNode(node, nodeEditor);
			}
		}
		return this;
	}

	public FlowEngineControl DetachNodeEditor()
	{
		lock (lifecycleLock)
		{
			DetachNodeEditorCore();
		}
		return this;
	}

	public FlowEngineControl DetachNodeEditor(STNodeEditor nodeEditor)
	{
		lock (lifecycleLock)
		{
			bool shouldDetach;
			lock (stateLock)
			{
				shouldDetach = nodeEditor != null && ReferenceEquals(NodeEditor, nodeEditor);
			}
			if (shouldDetach)
			{
				DetachNodeEditorCore();
			}
		}
		return this;
	}

	private void NodeEditor_NodeAdded(object sender, STNodeEditorEventArgs e)
	{
		InvalidateLoadedCanvas();
		RegisterNode(e.Node, sender as STNodeEditor);
	}

	private void NodeEditor_NodeRemoved(object sender, STNodeEditorEventArgs e)
	{
		UnregisterNode(e.Node);
		InvalidateLoadedCanvas();
	}

	private void NodeEditor_HistoryChanged(object sender, EventArgs e)
	{
		InvalidateLoadedCanvas();
	}

	private void NodeEditor_OptionChanged(object sender, STNodeEditorOptionEventArgs e)
	{
		InvalidateLoadedCanvas();
	}

	private void NodeEditor_NodeLocationChanged(object sender, EventArgs e)
	{
		InvalidateLoadedCanvas();
	}

	private void RegisterNode(STNode node, STNodeEditor expectedEditor)
	{
		lock (lifecycleLock)
		{
			RegisterNodeCore(node, expectedEditor);
		}
	}

	private void RegisterNodeCore(STNode node, STNodeEditor expectedEditor)
	{
		lock (stateLock)
		{
			if (isDisposed || !ReferenceEquals(NodeEditor, expectedEditor) || !attachedNodes.Add(node))
			{
				return;
			}
			node.PropertyChanged += AttachedNode_PropertyChanged;
		}
		if (node is BaseStartNode baseStartNode)
		{
			string generatedName = null;
			lock (stateLock)
			{
				if (isDisposed || !ReferenceEquals(NodeEditor, expectedEditor) || attachedStartNodes.Contains(baseStartNode))
				{
					return;
				}
				if (IsAutoStartName && !NodeEditor.IsReplayingHistory)
				{
					long ticks = DateTime.Now.Ticks;
					do
					{
						generatedName = ticks++.ToString();
					}
					while (attachedStartNodes.Any(item => item.NodeName == generatedName));
				}
			}
			if (generatedName != null)
			{
				baseStartNode.NodeName = generatedName;
			}
			lock (stateLock)
			{
				if (isDisposed || !ReferenceEquals(NodeEditor, expectedEditor) || !attachedNodes.Contains(baseStartNode)
					|| attachedStartNodes.Contains(baseStartNode))
				{
					return;
				}
				attachedStartNodes.Add(baseStartNode);
				baseStartNode.Finished += Start_Finished;
				RebuildStartNodeRegistryLocked();
			}
		}
		else if (node is CVBaseServerNode serverNode)
		{
			DeviceNode device;
			lock (stateLock)
			{
				if (isDisposed || !ReferenceEquals(NodeEditor, expectedEditor) || attachedDeviceNodes.ContainsKey(serverNode))
				{
					return;
				}
				device = new DeviceNode(serverNode);
				attachedDeviceNodes.Add(serverNode, device);
				RebuildServicesLocked();
			}
			AddDeviceIfCurrent(serverNode, device);
		}
	}

	private void UnregisterNode(STNode node)
	{
		lock (lifecycleLock)
		{
			UnregisterNodeCore(node);
		}
	}

	private void UnregisterNodeCore(STNode node)
	{
		BaseStartNode startToStop = null;
		DeviceNode deviceToRemove = null;
		lock (stateLock)
		{
			if (attachedNodes.Remove(node))
			{
				node.PropertyChanged -= AttachedNode_PropertyChanged;
			}
			if (node is BaseStartNode baseStartNode && attachedStartNodes.Remove(baseStartNode))
			{
				baseStartNode.Finished -= Start_Finished;
				RebuildStartNodeRegistryLocked();
				startToStop = baseStartNode;
			}
			else if (node is CVBaseServerNode serverNode && attachedDeviceNodes.Remove(serverNode, out DeviceNode device))
			{
				RebuildServicesLocked();
				deviceToRemove = device;
			}
		}
		if (startToStop != null)
		{
			StopStartNode(startToStop);
		}
		if (deviceToRemove != null)
		{
			NodeManager.RemoveDevice(deviceToRemove);
		}
	}

	private void AttachedNode_PropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		lock (stateLock)
		{
			if (sender is not STNode node || !attachedNodes.Contains(node))
			{
				return;
			}
			loadedCanvas.Clear();
		}
		if (sender is BaseStartNode startNode)
		{
			if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(CVCommonNode.NodeName))
			{
				lock (stateLock)
				{
					if (attachedStartNodes.Contains(startNode))
					{
						RebuildStartNodeRegistryLocked();
					}
				}
			}
			return;
		}
		if (sender is not CVBaseServerNode serverNode || !IsDeviceIdentityProperty(e.PropertyName))
		{
			return;
		}

		DeviceNode oldDevice;
		DeviceNode newDevice;
		lock (stateLock)
		{
			if (!attachedDeviceNodes.TryGetValue(serverNode, out oldDevice))
			{
				return;
			}
			newDevice = new DeviceNode(serverNode);
			attachedDeviceNodes[serverNode] = newDevice;
			RebuildServicesLocked();
		}
		NodeManager.RemoveDevice(oldDevice);
		AddDeviceIfCurrent(serverNode, newDevice);
	}

	private static bool IsDeviceIdentityProperty(string propertyName)
	{
		return string.IsNullOrEmpty(propertyName)
			|| propertyName == nameof(CVCommonNode.NodeType)
			|| propertyName == nameof(CVCommonNode.NodeName)
			|| propertyName == nameof(CVBaseServerNode.DeviceCode);
	}

	private void AddDeviceIfCurrent(CVBaseServerNode serverNode, DeviceNode device)
	{
		NodeManager.AddDevice(device);
		bool isCurrent;
		lock (stateLock)
		{
			isCurrent = attachedDeviceNodes.TryGetValue(serverNode, out DeviceNode current)
				&& ReferenceEquals(current, device);
		}
		if (!isCurrent)
		{
			NodeManager.RemoveDevice(device);
		}
	}

	private static void StopStartNode(BaseStartNode startNode)
	{
		try
		{
			startNode.StopAll();
		}
		catch (Exception ex)
		{
			logger.Error($"Failed to stop removed start node => {startNode.NodeName}", ex);
		}
	}

	private void RebuildStartNodeRegistryLocked()
	{
		startNodeNames.Clear();
		foreach (BaseStartNode startNode in attachedStartNodes)
		{
			if (string.IsNullOrEmpty(startNode.NodeName) || startNodeNames.ContainsKey(startNode.NodeName))
			{
				logger.WarnFormat("Ignoring duplicate or empty start node name => {0}", startNode.NodeName);
			}
			else
			{
				startNodeNames.Add(startNode.NodeName, startNode);
			}
		}
		_IsRunning = attachedStartNodes.Any(startNode => startNode.Running);
	}

	private void RebuildServicesLocked()
	{
		services.Clear();
		foreach (CVBaseServerNode serverNode in attachedDeviceNodes.Keys)
		{
			if (!services.ContainsKey(serverNode.NodeType))
			{
				services[serverNode.NodeType] = new ServiceNode(serverNode.NodeType);
			}
			services[serverNode.NodeType].AddMQTTService(serverNode);
		}
	}

	private void Start_Finished(object sender, FlowStartEventArgs e)
	{
		if (sender is not BaseStartNode baseStartNode)
		{
			return;
		}
		FlowEngineEventHandler finished;
		FlowEngineEventArgs args;
		lock (stateLock)
		{
			if (!attachedStartNodes.Contains(baseStartNode))
			{
				return;
			}
			_IsRunning = attachedStartNodes.Any(startNode => startNode.Running);
			finished = Finished;
			args = new FlowEngineEventArgs(baseStartNode.NodeName, e.SerialNumber, e.Status, e.TotalTime, e.Message, e.ErrorNodeName, e.ErrorNodeId);
		}
		finished?.Invoke(sender, args);
	}

	private void DetachNodeEditorCore()
	{
		STNodeEditor nodeEditor;
		lock (stateLock)
		{
			nodeEditor = NodeEditor;
			NodeEditor = null;
		}
		if (nodeEditor != null)
		{
			nodeEditor.NodeAdded -= NodeEditor_NodeAdded;
			nodeEditor.NodeRemoved -= NodeEditor_NodeRemoved;
			nodeEditor.OptionConnected -= NodeEditor_OptionChanged;
			nodeEditor.OptionDisConnected -= NodeEditor_OptionChanged;
			nodeEditor.NodeLocationChanged -= NodeEditor_NodeLocationChanged;
			nodeEditor.HistoryChanged -= NodeEditor_HistoryChanged;
		}
		ClearRegistrations();
	}

	private void ClearRegistrations()
	{
		BaseStartNode[] startNodes;
		DeviceNode[] devices;
		lock (stateLock)
		{
			startNodes = attachedStartNodes.ToArray();
			devices = attachedDeviceNodes.Values.ToArray();
			foreach (STNode node in attachedNodes)
			{
				node.PropertyChanged -= AttachedNode_PropertyChanged;
			}
			foreach (BaseStartNode startNode in startNodes)
			{
				startNode.Finished -= Start_Finished;
			}
			attachedNodes.Clear();
			attachedStartNodes.Clear();
			attachedDeviceNodes.Clear();
			startNodeNames.Clear();
			services.Clear();
			loadedCanvas.Clear();
			_IsRunning = false;
		}
		foreach (BaseStartNode startNode in startNodes)
		{
			StopStartNode(startNode);
		}
		foreach (DeviceNode device in devices)
		{
			NodeManager.RemoveDevice(device);
		}
	}

	public void LoadFromFile(string strFileName, List<MQTTServiceInfo> services)
	{
		STNodeEditor nodeEditor = GetNodeEditor();
		clear();
		try
		{
			nodeEditor.LoadCanvas(strFileName);
		}
		finally
		{
			nodeEditor.ClearHistory();
		}
		NodeManager.UpdateDevice(services);
	}

	public void LoadFromBase64(string base64Data, bool waitReady = false)
	{
		byte[] rawData = null;
		if (!string.IsNullOrEmpty(base64Data))
		{
			rawData = Convert.FromBase64String(base64Data);
		}
		Load(rawData, waitReady);
	}

	public void LoadFromBase64(string base64Data, List<MQTTServiceInfo> services, bool waitReady = false)
	{
		LoadFromBase64(base64Data, waitReady);
		NodeManager.UpdateDevice(services);
	}

	public void LoadFromBase64AndStart(string base64Data, string serialNumber, List<MQTTServiceInfo> services)
	{
		LoadFromBase64(base64Data, services, waitReady: true);
		StartNode(serialNumber, services);
	}

	public void FlowClear()
	{
		clear();
		STNodeEditor nodeEditor;
		lock (stateLock)
		{
			nodeEditor = NodeEditor;
		}
		nodeEditor?.ClearHistory();
	}

	private void clear()
	{
		BaseStartNode[] startNodes;
		STNodeEditor nodeEditor;
		lock (stateLock)
		{
			startNodes = attachedStartNodes.ToArray();
			nodeEditor = NodeEditor;
		}
		try
		{
			foreach (BaseStartNode startNode in startNodes)
			{
				StopStartNode(startNode);
			}
			nodeEditor?.Nodes.Clear();
		}
		finally
		{
			ClearRegistrations();
			foreach (BaseStartNode startNode in startNodes)
			{
				startNode.Dispose();
			}
		}
	}

	public void Load(byte[] rawData, bool waitReady)
	{
		STNodeEditor nodeEditor = GetNodeEditor();
		if (rawData != null)
		{
			string text = BitConverter.ToString(MD5.HashData(rawData));
			logger.DebugFormat("Load flow data={0}", text);
			lock (stateLock)
			{
				if (loadedCanvas.ContainsKey(text))
				{
					return;
				}
			}
			clear();
			try
			{
				nodeEditor.LoadCanvas(rawData);
			}
			finally
			{
				nodeEditor.ClearHistory();
			}
			lock (stateLock)
			{
				loadedCanvas[text] = rawData;
			}
			if (!waitReady)
			{
				return;
			}
			for (int i = 0; i < 10; i++)
			{
				if (IsReady)
				{
					break;
				}
				Thread.Sleep(200);
			}
		}
		else
		{
			clear();
			nodeEditor.ClearHistory();
		}
	}

	public string[] GetStartNodeNames()
	{
		lock (stateLock)
		{
			return startNodeNames.Keys.ToArray();
		}
	}

	public string GetStartNodeName()
	{
		lock (stateLock)
		{
			if (startNodeNames.Count > 0)
			{
				return startNodeNames.First().Key;
			}
			return null;
		}
	}

	public void StartNode(string serialNumber, List<MQTTServiceInfo> services)
	{
		FlowServiceManager.Instance.AddMQTTService(services);
		StartNode(serialNumber);
	}

	public void StartNode(string name, string serialNumber, List<MQTTServiceInfo> services)
	{
		FlowServiceManager.Instance.AddMQTTService(services);
		StartNode(name, serialNumber);
	}

	public void StartNode(string serialNumber)
	{
		StartNode(GetStartNodeName(), serialNumber);
	}

	protected void StartNode(string name, string serialNumber)
	{
		BaseStartNode startNode = null;
		lock (stateLock)
		{
			if (!_IsRunning && name != null && startNodeNames.TryGetValue(name, out startNode))
			{
				_IsRunning = true;
			}
		}
		if (startNode == null)
		{
			return;
		}
		try
		{
			logger.DebugFormat("Starting flow serialNumber={0}", serialNumber);
			startNode.Start(serialNumber);
		}
		finally
		{
			bool detached;
			lock (stateLock)
			{
				detached = !attachedStartNodes.Contains(startNode);
				_IsRunning = attachedStartNodes.Any(node => node.Running);
			}
			if (detached)
			{
				StopStartNode(startNode);
			}
		}
	}

	public void StopNode(string serialNumber)
	{
		StopNode(GetStartNodeName(), serialNumber);
	}

	public void StopNode(string name, string serialNumber)
	{
		BaseStartNode startNode;
		lock (stateLock)
		{
			startNodeNames.TryGetValue(name ?? string.Empty, out startNode);
		}
		if (startNode == null)
		{
			return;
		}
		startNode.Stop(serialNumber);
		lock (stateLock)
		{
			_IsRunning = attachedStartNodes.Any(node => node.Running);
		}
	}

	public void LoadFromFile(string strFileName)
	{
		throw new NotImplementedException();
	}

	public void Dispose()
	{
		lock (lifecycleLock)
		{
			lock (stateLock)
			{
				if (isDisposed)
				{
					return;
				}
				isDisposed = true;
			}
			DetachNodeEditorCore();
		}
		GC.SuppressFinalize(this);
	}

	private STNodeEditor GetNodeEditor()
	{
		lock (stateLock)
		{
			ThrowIfDisposedLocked();
			return NodeEditor ?? throw new InvalidOperationException("Attach an STNodeEditor before loading a flow.");
		}
	}

	private void InvalidateLoadedCanvas()
	{
		lock (stateLock)
		{
			loadedCanvas.Clear();
		}
	}

	private void ThrowIfDisposedLocked()
	{
		ObjectDisposedException.ThrowIf(isDisposed, this);
	}
}
