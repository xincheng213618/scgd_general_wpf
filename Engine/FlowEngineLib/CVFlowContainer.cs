using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using FlowEngineLib.Base;
using FlowEngineLib.Start;
using log4net;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

public class CVFlowContainer : CVNodeContainer
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(CVFlowContainer));

	protected Dictionary<string, byte[]> loadedCanvas;

	protected Dictionary<string, BaseStartNode> startNodeNames;

	protected Dictionary<string, string> startNodesFlowMap;

	protected Dictionary<string, ServiceNode> services;

	protected bool IsAutoStartName;

	public bool IsReady => GetFlowReady();

	public bool IsRunning => GetFlowRunning();

	public event FlowEngineEventHandler Finished;

	private bool GetFlowReady()
	{
		if (startNodeNames.Count > 0)
		{
			return startNodeNames.First().Value.Ready;
		}
		return false;
	}

	private bool GetFlowRunning()
	{
		bool flag = false;
		foreach (KeyValuePair<string, BaseStartNode> startNodeName in startNodeNames)
		{
			flag = flag || startNodeName.Value.Running;
		}
		return flag;
	}

	public CVFlowContainer(bool isAutoStartName)
	{
		startNodeNames = new Dictionary<string, BaseStartNode>();
		startNodesFlowMap = new Dictionary<string, string>();
		IsAutoStartName = isAutoStartName;
		services = new Dictionary<string, ServiceNode>();
		loadedCanvas = new Dictionary<string, byte[]>();
	}

	protected override void OnNodeAdded(STNodeEditorEventArgs e)
	{
		if (e.Node is BaseStartNode)
		{
			BaseStartNode baseStartNode = e.Node as BaseStartNode;
			if (IsAutoStartName)
			{
				baseStartNode.NodeName = DateTime.Now.Ticks.ToString();
			}
			startNodeNames.Add(baseStartNode.NodeName, baseStartNode);
			string text = string.Empty;
			foreach (KeyValuePair<string, string> item in startNodesFlowMap)
			{
				if (string.IsNullOrEmpty(item.Value))
				{
					text = item.Key;
					break;
				}
			}
			if (!string.IsNullOrEmpty(text))
			{
				startNodesFlowMap[text] = baseStartNode.NodeName;
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
		this.Finished?.Invoke(sender, new FlowEngineEventArgs(baseStartNode.NodeName, e.SerialNumber, e.Status, e.TotalTime, e.Message));
	}

	private void AddDevice(CVBaseServerNode node)
	{
		DeviceNode device = new DeviceNode(node);
		FlowNodeManager.Instance.AddDevice(device);
	}

	protected override void OnNodeRemoved(STNodeEditorEventArgs e)
	{
	}

	public void LoadFromBase64(string base64Data, List<MQTTServiceInfo> services, bool waitReady = false)
	{
		FlowNodeManager.Instance.ClearDevice();
		clear();
		LoadFromBase64(base64Data, waitReady);
		FlowNodeManager.Instance.UpdateDevice(services);
	}

	public void LoadFromBase64AndStart(string base64Data, string serialNumber, List<MQTTServiceInfo> services)
	{
		LoadFromBase64(base64Data, services, waitReady: true);
		StartNode(serialNumber, services);
	}

	public bool AppendFromBase64AndStart(string flowKey, string base64Data, string serialNumber, List<MQTTServiceInfo> services)
	{
		if (startNodesFlowMap.ContainsKey(flowKey))
		{
			logger.ErrorFormat("Flow is existed and is running => {0}", flowKey);
			return false;
		}
		startNodesFlowMap.Add(flowKey, string.Empty);
		AppendFromBase64(base64Data, waitReady: true);
		if (!WaitFlowLoaded(flowKey))
		{
			startNodesFlowMap.Remove(flowKey);
			logger.ErrorFormat("Flow is not ready => {0}", flowKey);
			return false;
		}
		FlowNodeManager.Instance.UpdateDevice(services);
		if (logger.IsInfoEnabled)
		{
			logger.InfoFormat("Flow begin running => {0}/{1}", flowKey, serialNumber);
		}
		StartNode(startNodesFlowMap[flowKey], serialNumber, services);
		return true;
	}

	private bool WaitFlowLoaded(string flowKey)
	{
		string text = startNodesFlowMap[flowKey];
		for (int i = 0; i < 20; i++)
		{
			if (!string.IsNullOrEmpty(text))
			{
				break;
			}
			Thread.Sleep(200);
			text = startNodesFlowMap[flowKey];
		}
		if (!string.IsNullOrEmpty(text) && startNodeNames.ContainsKey(text))
		{
			BaseStartNode baseStartNode = startNodeNames[text];
			for (int j = 0; j < 20; j++)
			{
				if (baseStartNode.Ready)
				{
					break;
				}
				Thread.Sleep(200);
			}
			return baseStartNode.Ready;
		}
		return false;
	}

	public void AppendFromBase64(string base64Data, bool waitReady = false)
	{
		byte[] rawData = null;
		if (!string.IsNullOrEmpty(base64Data))
		{
			rawData = Convert.FromBase64String(base64Data);
		}
		Load(rawData, waitReady);
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
			LoadCanvas(rawData);
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

	public void FlowClear()
	{
		clear();
	}

	private void clear()
	{
		base.Nodes.Clear();
		foreach (KeyValuePair<string, BaseStartNode> startNodeName in startNodeNames)
		{
			startNodeName.Value.Finished -= Start_Finished;
			startNodeName.Value.Dispose();
		}
		startNodeNames.Clear();
		services.Clear();
		loadedCanvas.Clear();
		startNodesFlowMap.Clear();
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
		if (!startNodeNames.ContainsKey(name))
		{
			return;
		}
		BaseStartNode baseStartNode = startNodeNames[name];
		if (!baseStartNode.Running)
		{
			if (logger.IsDebugEnabled)
			{
				logger.DebugFormat("Starting flow serialNumber={0}", serialNumber);
			}
			baseStartNode.Start(serialNumber);
		}
	}

	public void StopNode(string serialNumber)
	{
		StopNode(GetStartNodeName(), serialNumber);
	}

	public void StopAllNode()
	{
		foreach (KeyValuePair<string, BaseStartNode> startNodeName in startNodeNames)
		{
			startNodeName.Value.StopAll();
		}
	}

	public void StopNode(string name, string serialNumber)
	{
		if (!string.IsNullOrEmpty(name) && startNodeNames.ContainsKey(name))
		{
			startNodeNames[name].Stop(serialNumber);
		}
	}
}
