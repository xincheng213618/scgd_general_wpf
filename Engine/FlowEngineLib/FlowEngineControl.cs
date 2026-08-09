using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using FlowEngineLib.Base;
using FlowEngineLib.Runtime;
using FlowEngineLib.Start;
using log4net;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

public class FlowEngineControl : FlowEngineAPI, IDisposable
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(FlowEngineControl));

	protected STNodeEditor NodeEditor;

	private IFlowGraphHost graphHost;

	protected Dictionary<string, BaseStartNode> startNodeNames;

	protected Dictionary<string, ServiceNode> services;

	protected Dictionary<string, byte[]> loadedCanvas;

	protected bool IsAutoStartName;

	protected bool _IsRunning;

	protected readonly FlowNodeManager NodeManager;

	private readonly List<BaseStartNode> attachedStartNodes;

	private readonly Dictionary<CVBaseServerNode, DeviceNode> attachedDeviceNodes;

	private readonly HashSet<STNode> attachedNodes;

	// FLOW_EXECUTION_POLICY_RESTORE_POINT: optional retry and ERROR-route
	// policies were removed; recover the feature from commit 3ca350bcd.

	private readonly IFlowServiceResolver runtimeServiceResolver;

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
				return startNodeNames.First().Value.IsExecutionReady;
			}
			return false;
		}
	}

	public bool IsStartNodeReady(string name)
	{
		lock (stateLock)
		{
			return name != null
				&& startNodeNames.TryGetValue(name, out BaseStartNode startNode)
				&& startNode.IsExecutionReady;
		}
	}

	public bool CanStartNode(string name)
	{
		lock (stateLock)
		{
			return !_IsRunning
				&& name != null
				&& startNodeNames.TryGetValue(name, out BaseStartNode startNode)
				&& startNode.IsExecutionReady
				&& startNode.CanAcceptStart;
		}
	}

	public async Task<bool> EnsureStartNodeReadyAsync(string name, TimeSpan timeout, CancellationToken cancellationToken = default)
	{
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

		BaseStartNode startNode;
		lock (stateLock)
		{
			if (name == null || !startNodeNames.TryGetValue(name, out startNode))
			{
				return false;
			}
		}

		if (!startNode.RequiresConnectionReady)
		{
			return true;
		}

		using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutSource.CancelAfter(timeout);
		try
		{
			if (!await startNode.EnsureReadyAsync(timeoutSource.Token).ConfigureAwait(false))
			{
				return false;
			}
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			logger.WarnFormat("Timed out waiting for start node readiness => {0}, timeout={1}ms", name, timeout.TotalMilliseconds);
			return false;
		}

		lock (stateLock)
		{
			return attachedStartNodes.Contains(startNode)
				&& startNodeNames.TryGetValue(name, out BaseStartNode current)
				&& ReferenceEquals(current, startNode)
				&& startNode.IsExecutionReady;
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

	public FlowEngineControl(CVNodeContainer nodeContainer, bool isAutoStartName)
		: this(nodeContainer, isAutoStartName, FlowNodeManager.Instance)
	{
	}

	public FlowEngineControl(CVNodeContainer nodeContainer, bool isAutoStartName, FlowNodeManager nodeManager)
		: this(isAutoStartName, nodeManager)
	{
		AttachNodeContainer(nodeContainer);
	}

	internal FlowEngineControl(
		CVNodeContainer nodeContainer,
		bool isAutoStartName,
		FlowNodeManager nodeManager,
		IFlowServiceResolver runtimeServiceResolver)
		: this(
			isAutoStartName,
			nodeManager,
			runtimeServiceResolver)
	{
		AttachNodeContainer(nodeContainer);
	}

	public FlowEngineControl(bool isAutoStartName)
		: this(isAutoStartName, FlowNodeManager.Instance)
	{
	}

	public FlowEngineControl(bool isAutoStartName, FlowNodeManager nodeManager)
		: this(
			isAutoStartName,
			nodeManager,
			runtimeServiceResolver: null)
	{
	}

	private FlowEngineControl(
		bool isAutoStartName,
		FlowNodeManager nodeManager,
		IFlowServiceResolver runtimeServiceResolver)
	{
		NodeManager = nodeManager ?? throw new ArgumentNullException(nameof(nodeManager));
		this.runtimeServiceResolver = runtimeServiceResolver;
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
			AttachGraphHostCore(new EditorFlowGraphHost(nodeEditor), nodeEditor);
		}
		return this;
	}

	public FlowEngineControl AttachNodeContainer(CVNodeContainer nodeContainer)
	{
		if (nodeContainer == null)
		{
			throw new ArgumentNullException(nameof(nodeContainer));
		}
		lock (lifecycleLock)
		{
			lock (stateLock)
			{
				ThrowIfDisposedLocked();
				if (graphHost is HeadlessFlowGraphHost current
					&& ReferenceEquals(current.Container, nodeContainer))
				{
					return this;
				}
			}
			DetachNodeEditorCore();
			AttachGraphHostCore(new HeadlessFlowGraphHost(nodeContainer), null);
		}
		return this;
	}

	private void AttachGraphHostCore(IFlowGraphHost host, STNodeEditor nodeEditor)
	{
		lock (stateLock)
		{
			ThrowIfDisposedLocked();
			graphHost = host;
			NodeEditor = nodeEditor;
			host.NodeAdded += NodeEditor_NodeAdded;
			host.NodeRemoved += NodeEditor_NodeRemoved;
			host.OptionConnected += NodeEditor_OptionChanged;
			host.OptionDisconnected += NodeEditor_OptionChanged;
			host.NodeLocationChanged += NodeEditor_NodeLocationChanged;
			host.HistoryChanged += NodeEditor_HistoryChanged;
		}
		foreach (STNode node in host.Nodes)
		{
			RegisterNode(node, host);
		}
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
		IFlowGraphHost host;
		lock (stateLock)
		{
			host = graphHost;
		}
		if (host != null && host.IsEventSource(sender))
		{
			RegisterNode(e.Node, host);
		}
	}

	private void NodeEditor_NodeRemoved(object sender, STNodeEditorEventArgs e)
	{
		if (!IsCurrentGraphEventSource(sender))
		{
			return;
		}
		UnregisterNode(e.Node);
		InvalidateLoadedCanvas();
	}

	private void NodeEditor_HistoryChanged(object sender, EventArgs e)
	{
		if (IsCurrentGraphEventSource(sender))
		{
			InvalidateLoadedCanvas();
		}
	}

	private void NodeEditor_OptionChanged(object sender, STNodeEditorOptionEventArgs e)
	{
		if (IsCurrentGraphEventSource(sender))
		{
			InvalidateLoadedCanvas();
		}
	}

	private void NodeEditor_NodeLocationChanged(object sender, EventArgs e)
	{
		if (IsCurrentGraphEventSource(sender))
		{
			InvalidateLoadedCanvas();
		}
	}

	private bool IsCurrentGraphEventSource(object sender)
	{
		lock (stateLock)
		{
			return graphHost != null && graphHost.IsEventSource(sender);
		}
	}

	private void RegisterNode(STNode node, IFlowGraphHost expectedHost)
	{
		lock (lifecycleLock)
		{
			RegisterNodeCore(node, expectedHost);
		}
	}

	private void RegisterNodeCore(STNode node, IFlowGraphHost expectedHost)
	{
		lock (stateLock)
		{
			if (isDisposed || !ReferenceEquals(graphHost, expectedHost) || !attachedNodes.Add(node))
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
				if (isDisposed || !ReferenceEquals(graphHost, expectedHost) || attachedStartNodes.Contains(baseStartNode))
				{
					return;
				}
				if (IsAutoStartName && !expectedHost.IsReplayingChanges)
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
				if (isDisposed || !ReferenceEquals(graphHost, expectedHost) || !attachedNodes.Contains(baseStartNode)
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
				if (isDisposed || !ReferenceEquals(graphHost, expectedHost) || attachedDeviceNodes.ContainsKey(serverNode))
				{
					return;
				}
				device = new DeviceNode(serverNode);
				serverNode.RuntimeServiceResolver =
					runtimeServiceResolver;
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
				serverNode.RuntimeServiceResolver = null;
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
			args = new FlowEngineEventArgs(
				baseStartNode.NodeName,
				e.SerialNumber,
				e.Status,
				e.TotalTime,
				e.Message,
				e.ErrorNodeName,
				e.ErrorNodeId);
		}
		Delegate[] handlers = finished?.GetInvocationList() ?? Array.Empty<Delegate>();
		foreach (FlowEngineEventHandler handler in handlers.Cast<FlowEngineEventHandler>())
		{
			try
			{
				handler(sender, args);
			}
			catch (Exception ex)
			{
				logger.Error("Flow completion subscriber failed.", ex);
			}
		}
	}

	private void DetachNodeEditorCore()
	{
		IFlowGraphHost host;
		lock (stateLock)
		{
			host = graphHost;
			graphHost = null;
			NodeEditor = null;
		}
		if (host != null)
		{
			host.NodeAdded -= NodeEditor_NodeAdded;
			host.NodeRemoved -= NodeEditor_NodeRemoved;
			host.OptionConnected -= NodeEditor_OptionChanged;
			host.OptionDisconnected -= NodeEditor_OptionChanged;
			host.NodeLocationChanged -= NodeEditor_NodeLocationChanged;
			host.HistoryChanged -= NodeEditor_HistoryChanged;
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
				if (node is CVBaseServerNode serverNode)
				{
					serverNode.RuntimeServiceResolver = null;
				}
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
		LoadFromFile(strFileName);
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
		IFlowGraphHost host;
		lock (stateLock)
		{
			host = graphHost;
		}
		host?.ClearHistory();
	}

	private void clear()
	{
		BaseStartNode[] startNodes;
		IFlowGraphHost host;
		lock (stateLock)
		{
			startNodes = attachedStartNodes.ToArray();
			host = graphHost;
		}
		try
		{
			foreach (BaseStartNode startNode in startNodes)
			{
				StopStartNode(startNode);
			}
			host?.Clear();
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
		IFlowGraphHost host = GetGraphHost();
		if (rawData != null)
		{
			string text = BitConverter.ToString(MD5.HashData(rawData));
			logger.DebugFormat("Load flow data={0}", text);
			bool alreadyLoaded;
			lock (stateLock)
			{
				alreadyLoaded = loadedCanvas.ContainsKey(text);
			}
			if (alreadyLoaded)
			{
				if (waitReady)
				{
					WaitUntilReady();
				}
				return;
			}
			try
			{
				ReplaceCanvas(host, rawData);
			}
			finally
			{
				host.ClearHistory();
			}
			lock (stateLock)
			{
				loadedCanvas[text] = rawData;
			}
			if (!waitReady)
			{
				return;
			}
			WaitUntilReady();
		}
		else
		{
			clear();
			host.ClearHistory();
		}
	}

	private void WaitUntilReady()
	{
		for (int i = 0; i < 10 && !IsReady; i++)
		{
			Thread.Sleep(200);
		}
	}

	private void ReplaceCanvas(IFlowGraphHost host, byte[] rawData)
	{
		BaseStartNode[] previousStartNodes;
		lock (stateLock)
		{
			previousStartNodes = attachedStartNodes.ToArray();
		}

		host.LoadCanvas(rawData);
		foreach (BaseStartNode startNode in previousStartNodes)
		{
			try
			{
				startNode.Dispose();
			}
			catch (Exception ex)
			{
				logger.Warn($"Failed to dispose replaced start node => {startNode.NodeName}", ex);
			}
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

	public bool TryStartNode(string serialNumber, List<MQTTServiceInfo> services)
	{
		FlowServiceManager.Instance.AddMQTTService(services);
		return TryStartNode(GetStartNodeName(), serialNumber);
	}

	public bool TryStartNode(string name, string serialNumber, List<MQTTServiceInfo> services)
	{
		FlowServiceManager.Instance.AddMQTTService(services);
		return TryStartNode(name, serialNumber);
	}

	protected void StartNode(string name, string serialNumber)
	{
		TryStartNode(name, serialNumber);
	}

	public bool TryStartNode(string name, string serialNumber)
	{
		BaseStartNode startNode = null;
		lock (stateLock)
		{
			if (!_IsRunning
				&& name != null
				&& startNodeNames.TryGetValue(name, out startNode)
				&& startNode.IsExecutionReady
				&& startNode.CanAcceptStart)
			{
				_IsRunning = true;
			}
			else
			{
				startNode = null;
			}
		}
		if (startNode == null)
		{
			logger.WarnFormat("Flow start rejected because the start node is missing, busy, or not ready => {0}", name);
			return false;
		}
		bool started = false;
		try
		{
			logger.DebugFormat("Starting flow serialNumber={0}", serialNumber);
			started = startNode.TryStart(serialNumber);
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
		return started;
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
		ArgumentException.ThrowIfNullOrWhiteSpace(strFileName);
		Load(System.IO.File.ReadAllBytes(strFileName), waitReady: false);
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

	private IFlowGraphHost GetGraphHost()
	{
		lock (stateLock)
		{
			ThrowIfDisposedLocked();
			return graphHost ?? throw new InvalidOperationException(
				"Attach an STNodeEditor or CVNodeContainer before loading a flow.");
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
