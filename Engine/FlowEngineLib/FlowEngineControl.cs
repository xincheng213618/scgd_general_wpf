using System;
using System.Collections.Generic;
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

	private bool isDisposed;

	public bool IsReady => GetFlowReady();

	public bool IsRunning => _IsRunning;

	public event FlowEngineEventHandler Finished;

	private bool GetFlowReady()
	{
		if (startNodeNames.Count > 0)
		{
			return startNodeNames.First().Value.Ready;
		}
		return false;
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
		_IsRunning = false;
	}

	public FlowEngineControl AttachNodeEditor(STNodeEditor nodeEditor)
	{
		ThrowIfDisposed();
		if (nodeEditor == null)
		{
			throw new ArgumentNullException(nameof(nodeEditor));
		}
		if (ReferenceEquals(NodeEditor, nodeEditor))
		{
			return this;
		}
		DetachNodeEditorCore();
		NodeEditor = nodeEditor;
		NodeEditor.NodeAdded += NodeEditor_NodeAdded;
		NodeEditor.NodeRemoved += NodeEditor_NodeRemoved;
		NodeEditor.HistoryChanged += NodeEditor_HistoryChanged;
		foreach (STNode node in NodeEditor.Nodes)
		{
			RegisterNode(node);
		}
		return this;
	}

	public FlowEngineControl DetachNodeEditor()
	{
		DetachNodeEditorCore();
		return this;
	}

	public FlowEngineControl DetachNodeEditor(STNodeEditor nodeEditor)
	{
		if (nodeEditor != null && ReferenceEquals(NodeEditor, nodeEditor))
		{
			DetachNodeEditorCore();
		}
		return this;
	}

	private void NodeEditor_NodeAdded(object sender, STNodeEditorEventArgs e)
	{
		loadedCanvas.Clear();
		RegisterNode(e.Node);
	}

	private void NodeEditor_NodeRemoved(object sender, STNodeEditorEventArgs e)
	{
		UnregisterNode(e.Node);
		loadedCanvas.Clear();
	}

	private void NodeEditor_HistoryChanged(object sender, EventArgs e)
	{
		loadedCanvas.Clear();
	}

	private void RegisterNode(STNode node)
	{
		if (node is BaseStartNode baseStartNode)
		{
			if (attachedStartNodes.Contains(baseStartNode))
			{
				return;
			}
			if (IsAutoStartName && !NodeEditor.IsReplayingHistory)
			{
				long ticks = DateTime.Now.Ticks;
				do
				{
					baseStartNode.NodeName = ticks++.ToString();
				}
				while (attachedStartNodes.Any(node => node.NodeName == baseStartNode.NodeName));
			}
			attachedStartNodes.Add(baseStartNode);
			RebuildStartNodeRegistry();
		}
		else if (node is CVBaseServerNode cVBaseServerNode)
		{
			if (!attachedDeviceNodes.ContainsKey(cVBaseServerNode))
			{
				DeviceNode device = new DeviceNode(cVBaseServerNode);
				attachedDeviceNodes.Add(cVBaseServerNode, device);
				NodeManager.AddDevice(device);
			}
			RebuildServices();
		}
	}

	private void UnregisterNode(STNode node)
	{
		if (node is BaseStartNode baseStartNode)
		{
			StopStartNode(baseStartNode);
			baseStartNode.Finished -= Start_Finished;
			if (attachedStartNodes.Remove(baseStartNode))
			{
				RebuildStartNodeRegistry();
			}
		}
		else if (node is CVBaseServerNode cVBaseServerNode && attachedDeviceNodes.Remove(cVBaseServerNode, out DeviceNode device))
		{
			NodeManager.RemoveDevice(device);
			RebuildServices();
		}
	}

	private static void StopStartNode(BaseStartNode startNode)
	{
		if (!startNode.Running)
		{
			return;
		}
		try
		{
			startNode.StopAll();
		}
		catch (Exception ex)
		{
			logger.Error($"Failed to stop removed start node => {startNode.NodeName}", ex);
		}
		finally
		{
			startNode.Running = false;
		}
	}

	private void RebuildStartNodeRegistry()
	{
		foreach (BaseStartNode startNode in attachedStartNodes)
		{
			startNode.Finished -= Start_Finished;
		}
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
			startNode.Finished += Start_Finished;
		}
		_IsRunning = attachedStartNodes.Any(startNode => startNode.Running);
	}

	private void RebuildServices()
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
		BaseStartNode baseStartNode = sender as BaseStartNode;
		_IsRunning = attachedStartNodes.Any(startNode => startNode.Running);
		this.Finished?.Invoke(sender, new FlowEngineEventArgs(baseStartNode.NodeName, e.SerialNumber, e.Status, e.TotalTime, e.Message, e.ErrorNodeName));
	}

	private void DetachNodeEditorCore()
	{
		if (NodeEditor != null)
		{
			NodeEditor.NodeAdded -= NodeEditor_NodeAdded;
			NodeEditor.NodeRemoved -= NodeEditor_NodeRemoved;
			NodeEditor.HistoryChanged -= NodeEditor_HistoryChanged;
			NodeEditor = null;
		}
		ClearRegistrations();
	}

	private void ClearRegistrations()
	{
		foreach (BaseStartNode startNode in attachedStartNodes)
		{
			startNode.Finished -= Start_Finished;
			StopStartNode(startNode);
		}
		foreach (DeviceNode device in attachedDeviceNodes.Values)
		{
			NodeManager.RemoveDevice(device);
		}
		attachedStartNodes.Clear();
		attachedDeviceNodes.Clear();
		startNodeNames.Clear();
		services.Clear();
		loadedCanvas.Clear();
		_IsRunning = false;
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
		NodeEditor?.ClearHistory();
	}

	private void clear()
	{
		BaseStartNode[] startNodes = attachedStartNodes.ToArray();
		try
		{
			foreach (BaseStartNode startNode in startNodes)
			{
				StopStartNode(startNode);
			}
			NodeEditor?.Nodes.Clear();
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
			string text = BitConverter.ToString(MD5.Create().ComputeHash(rawData));
			logger.DebugFormat("Load flow data={0}", text);
			if (loadedCanvas.ContainsKey(text))
			{
				return;
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
			loadedCanvas.Add(text, rawData);
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
		return startNodeNames.Keys.ToArray();
	}

	public string GetStartNodeName()
	{
		if (startNodeNames.Count > 0)
		{
			return startNodeNames.First().Key;
		}
		return null;
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
		if (!_IsRunning && startNodeNames.ContainsKey(name))
		{
			BaseStartNode baseStartNode = startNodeNames[name];
			_IsRunning = true;
			logger.DebugFormat("Starting flow serialNumber={0}", serialNumber);
			baseStartNode.Start(serialNumber);
		}
	}

	public void StopNode(string serialNumber)
	{
		StopNode(GetStartNodeName(), serialNumber);
	}

	public void StopNode(string name, string serialNumber)
	{
		if (startNodeNames.ContainsKey(name))
		{
			startNodeNames[name].Stop(serialNumber);
			_IsRunning = false;
		}
	}

	public void LoadFromFile(string strFileName)
	{
		throw new NotImplementedException();
	}

	public void Dispose()
	{
		if (isDisposed)
		{
			return;
		}
		DetachNodeEditorCore();
		isDisposed = true;
		GC.SuppressFinalize(this);
	}

	private STNodeEditor GetNodeEditor()
	{
		ThrowIfDisposed();
		return NodeEditor ?? throw new InvalidOperationException("Attach an STNodeEditor before loading a flow.");
	}

	private void ThrowIfDisposed()
	{
		ObjectDisposedException.ThrowIf(isDisposed, this);
	}
}
