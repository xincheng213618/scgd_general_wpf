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

public class FlowEngineControl : FlowEngineAPI
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(FlowEngineControl));

	protected STNodeEditor NodeEditor;

	protected Dictionary<string, BaseStartNode> startNodeNames;

	protected Dictionary<string, ServiceNode> services;

	protected Dictionary<string, byte[]> loadedCanvas;

	protected bool IsAutoStartName;

	protected bool _IsRunning;

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
		: this(isAutoStartName)
	{
		AttachNodeEditor(nodeEditor);
	}

	public FlowEngineControl(bool isAutoStartName)
	{
		startNodeNames = new Dictionary<string, BaseStartNode>();
		IsAutoStartName = isAutoStartName;
		services = new Dictionary<string, ServiceNode>();
		loadedCanvas = new Dictionary<string, byte[]>();
		_IsRunning = false;
	}

	public FlowEngineControl AttachNodeEditor(STNodeEditor nodeEditor)
	{
		NodeEditor = nodeEditor;
		NodeEditor.NodeAdded += NodeEditor_NodeAdded;
		return this;
	}

	private void NodeEditor_NodeAdded(object sender, STNodeEditorEventArgs e)
	{
		if (e.Node is BaseStartNode)
		{
			BaseStartNode baseStartNode = e.Node as BaseStartNode;
			if (IsAutoStartName)
			{
				baseStartNode.NodeName = DateTime.Now.Ticks.ToString();
			}
			if (!startNodeNames.ContainsKey(baseStartNode.NodeName))
			{
				startNodeNames.Add(baseStartNode.NodeName, baseStartNode);
			}
			baseStartNode.Finished += Start_Finished;
		}
		else if (e.Node is CVBaseServerNode)
		{
			CVBaseServerNode cVBaseServerNode = e.Node as CVBaseServerNode;
			if (!services.ContainsKey(cVBaseServerNode.NodeType))
			{
				services[cVBaseServerNode.NodeType] = new ServiceNode(cVBaseServerNode.NodeType);
			}
			services[cVBaseServerNode.NodeType].AddMQTTService(cVBaseServerNode);
			AddDevice(cVBaseServerNode);
		}
	}

	private void Start_Finished(object sender, FlowStartEventArgs e)
	{
		BaseStartNode baseStartNode = sender as BaseStartNode;
		_IsRunning = false;
		this.Finished?.Invoke(sender, new FlowEngineEventArgs(baseStartNode.NodeName, e.SerialNumber, e.Status, e.TotalTime, e.Message));
	}

	private void AddDevice(CVBaseServerNode node)
	{
		DeviceNode device = new DeviceNode(node);
		FlowNodeManager.Instance.AddDevice(device);
	}

	public void LoadFromFile(string strFileName, List<MQTTServiceInfo> services)
	{
		clear();
		FlowNodeManager.Instance.ClearDevice();
		NodeEditor.LoadCanvas(strFileName);
		FlowNodeManager.Instance.UpdateDevice(services);
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
		FlowNodeManager.Instance.ClearDevice();
		LoadFromBase64(base64Data, waitReady);
		FlowNodeManager.Instance.UpdateDevice(services);
	}

	public void LoadFromBase64AndStart(string base64Data, string serialNumber, List<MQTTServiceInfo> services)
	{
		LoadFromBase64(base64Data, services, waitReady: true);
		StartNode(serialNumber, services);
	}

	public void FlowClear()
	{
		clear();
	}

	private void clear()
	{
		NodeEditor.Nodes.Clear();
		foreach (KeyValuePair<string, BaseStartNode> startNodeName in startNodeNames)
		{
			startNodeName.Value.Finished -= Start_Finished;
			startNodeName.Value.Dispose();
		}
		startNodeNames.Clear();
		services.Clear();
		loadedCanvas.Clear();
		_IsRunning = false;
	}

	public void Load(byte[] rawData, bool waitReady)
	{
		if (rawData != null)
		{
			string text = BitConverter.ToString(MD5.Create().ComputeHash(rawData));
			logger.DebugFormat("Load flow data={0}", text);
			if (loadedCanvas.ContainsKey(text))
			{
				return;
			}
			clear();
			NodeEditor.LoadCanvas(rawData);
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
}
