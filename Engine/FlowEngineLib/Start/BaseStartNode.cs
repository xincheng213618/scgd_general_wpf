using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using FlowEngineLib.Base;
using FlowEngineLib.MQTT;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Start;

public abstract class BaseStartNode : CVCommonNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(BaseStartNode));

	public STNodeOption m_op_start;

	protected STNodeOption[] m_op_loop;

	protected Dictionary<string, CVStartCFC> startActions;

	protected Dictionary<string, List<CVBaseServerNode>> topicServer;

	protected Dictionary<string, List<CVServiceProxy>> topicServerProxy;

	public bool Ready { get; set; }

	public bool Running { get; set; }

	public event FlowStartEventHandler Finished;

	protected BaseStartNode(string title)
		: base(title, "StartNode", "S1", "DEV01")
	{
		topicServer = new Dictionary<string, List<CVBaseServerNode>>();
		topicServerProxy = new Dictionary<string, List<CVServiceProxy>>();
		startActions = new Dictionary<string, CVStartCFC>();
		base.AutoSize = false;
		base.Width = 170;
		Ready = false;
		Running = false;
		base.Height = 80;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		base.TitleColor = Color.FromArgb(200, Color.Goldenrod);
		m_op_start = new STNodeOption("OUT_START", typeof(CVStartCFC), bSingle: false);
		base.OutputOptions.Add(m_op_start);
		m_op_loop = new STNodeOption[2];
		m_op_loop[0] = base.OutputOptions.Add("OUT_LOOP1", typeof(CVLoopCFC), bSingle: false);
		m_op_loop[1] = base.OutputOptions.Add("OUT_LOOP2", typeof(CVLoopCFC), bSingle: false);
		m_op_start.Connected += m_op_start_Connected;
		m_op_start.DisConnected += m_op_start_DisConnected;
		m_op_loop[0].Connected += m_op_loop_Connected;
		m_op_loop[1].Connected += m_op_loop_Connected;
		m_op_loop[0].DisConnected += m_op_loop_DisConnected;
		m_op_loop[1].DisConnected += m_op_loop_DisConnected;
		ThreadPool.SetMinThreads(1, 1);
		ThreadPool.SetMaxThreads(5, 5);
	}

	public void DoLoopNextAction(CVLoopCFC next)
	{
		CVStartCFC cFC = GetCFC(next.SerialNumber);
		if (cFC != null)
		{
			string msg = cFC.BuildStatusMsg(base.NodeName, base.DeviceCode, -1);
			DoPublishStatus(msg);
		}
		STNodeOption[] op_loop = m_op_loop;
		foreach (STNodeOption sTNodeOption in op_loop)
		{
			if (IsEqualsNodeName(sTNodeOption, next.NodeName))
			{
				DoLoopTransferData(sTNodeOption, next);
				break;
			}
		}
	}

	private bool IsEqualsNodeName(STNodeOption opst, string nodeName)
	{
		for (int i = 0; i < opst.ConnectionCount; i++)
		{
			STNodeOption sTNodeOption = opst.ConnectedOption.ElementAt(i);
			if (typeof(LoopNode).IsAssignableFrom(sTNodeOption.Owner.GetType()))
			{
				LoopNode loopNode = sTNodeOption.Owner as LoopNode;
				if (nodeName.Equals(loopNode.NodeName))
				{
					return true;
				}
			}
			else if (typeof(ICVLoopNextNode).IsAssignableFrom(sTNodeOption.Owner.GetType()))
			{
				ICVLoopNextNode iCVLoopNextNode = sTNodeOption.Owner as ICVLoopNextNode;
				if (nodeName.Equals(iCVLoopNextNode.LoopName))
				{
					return true;
				}
			}
		}
		return false;
	}

	protected void m_op_loop_DisConnected(object sender, STNodeOptionEventArgs e)
	{
		STNodeOption sTNodeOption = sender as STNodeOption;
		SetOptionText(sTNodeOption, "OUT_LOOP" + base.OutputOptions.IndexOf(sTNodeOption));
		DoLoopDisConnected(sTNodeOption, e);
	}

	protected void m_op_loop_Connected(object sender, STNodeOptionEventArgs e)
	{
		STNodeOption sTNodeOption = sender as STNodeOption;
		SetOptionText(sTNodeOption, "--");
		DoLoopConnected(sTNodeOption, e);
	}

	protected virtual void DoLoopDisConnected(STNodeOption op, STNodeOptionEventArgs e)
	{
	}

	protected virtual void DoLoopConnected(STNodeOption sender, STNodeOptionEventArgs e)
	{
	}

	protected virtual void DoStartConnected(STNodeOption sender, STNodeOptionEventArgs e)
	{
	}

	protected virtual void DoStartDisConnected(STNodeOption sender, STNodeOptionEventArgs e)
	{
	}

	protected virtual void DoStatusTransferData(CVStartCFC action)
	{
	}

	private void m_op_start_DisConnected(object sender, STNodeOptionEventArgs e)
	{
		STNodeOption sTNodeOption = sender as STNodeOption;
		SetOptionText(sTNodeOption, "OUT_START");
		DoStartDisConnected(sTNodeOption, e);
	}

	private void m_op_start_Connected(object sender, STNodeOptionEventArgs e)
	{
		STNodeOption sTNodeOption = sender as STNodeOption;
		SetOptionText(sTNodeOption, "--");
		DoStartConnected(sTNodeOption, e);
	}

	public virtual void DoLoopTransferData(STNodeOption sender, CVLoopCFC next)
	{
		if (sender.ConnectionCount > 0)
		{
			SetOptionText(sender, DateTime.Now.ToString("HH:mm:ss:fffff"));
			ThreadPool.QueueUserWorkItem(DoLoopNextTask, new KeyValuePair<STNodeOption, CVLoopCFC>(sender, next));
		}
	}

	private void DoLoopNextTask(object obj)
	{
		KeyValuePair<STNodeOption, CVLoopCFC> keyValuePair = (KeyValuePair<STNodeOption, CVLoopCFC>)obj;
		STNodeOption key = keyValuePair.Key;
		CVLoopCFC value = keyValuePair.Value;
		key.TransferData(value);
		DoLoopNextAction(value, value.NodeName);
	}

	protected virtual void DoLoopNextAction(CVLoopCFC next, string nodeName)
	{
	}

	private void Startup(CVStartCFC action)
	{
		SetOptionText(m_op_start, action.SerialNumber);
		startActions.Add(action.SerialNumber, action);
		m_op_start.TransferData(action);
	}

	protected virtual void DoStartTransferData(CVStartCFC action)
	{
		if (m_op_start.ConnectionCount <= 0)
		{
			return;
		}
		if (action != null)
		{
			if (!startActions.ContainsKey(action.SerialNumber))
			{
				if (action.IsRunning)
				{
					if (startActions.Count == 0)
					{
						Startup(action);
					}
					else
					{
						action.SetStatusType(StatusTypeEnum.Failed);
					}
				}
				else
				{
					action.SetStatusType(StatusTypeEnum.Failed);
				}
				DoStatusTransferData(action);
				return;
			}
			CVStartCFC cVStartCFC = startActions[action.SerialNumber];
			if (action.IsStop)
			{
				cVStartCFC.SetActionType(action.GetActionType());
				cVStartCFC.EndTime = DateTime.Now;
				RemoveStartAction(cVStartCFC);
			}
			else if (cVStartCFC.GetActionType() != action.GetActionType())
			{
				cVStartCFC.SetActionType(action.GetActionType());
				SetOptionText(m_op_start, cVStartCFC.SerialNumber);
				m_op_start.TransferData(cVStartCFC);
			}
			DoStatusTransferData(cVStartCFC);
		}
		else
		{
			SetOptionText(m_op_start, "--");
			m_op_start.TransferData(action);
		}
	}

	private void RemoveStartAction(CVStartCFC action)
	{
		string serialNumber = action.SerialNumber;
		if (startActions.ContainsKey(serialNumber))
		{
			startActions.Remove(serialNumber);
			TimeSpan totalTime = action.GetTotalTime();
			SetOptionText(m_op_start, $"{totalTime.TotalSeconds:F4}s" + "/--");
			for (int i = 0; i < 2; i++)
			{
				if (m_op_loop[i].ConnectionCount > 0)
				{
					SetOptionText(m_op_loop[i], "--");
				}
			}
			if (logger.IsInfoEnabled)
			{
				logger.InfoFormat("Remove Flow Mapping => {0}", serialNumber);
			}
			m_op_start.TransferData(action);
		}
		else
		{
			logger.WarnFormat("Flow does not exist and has been removed. => {0}", action.ToShortString());
		}
	}

	public virtual void DoFinishing(CVStartCFC startAction)
	{
		RemoveStartAction(startAction);
		string msg = startAction.BuildStatusMsg(m_nodeName, m_deviceCode, -1);
		DoPublishStatus(msg);
		Running = false;
	}

	public CVStartCFC GetCFC(string serialNumber)
	{
		if (startActions.ContainsKey(serialNumber))
		{
			return startActions[serialNumber];
		}
		return null;
	}

	public virtual void DoPublishStatus(string msg)
	{
	}

	public virtual void DoPublish(MQActionEvent act)
	{
	}

	public virtual void DoSubscribe(string topic, CVBaseServerNode serverNode)
	{
		if (topicServer.ContainsKey(topic))
		{
			List<CVBaseServerNode> list = topicServer[topic];
			foreach (CVBaseServerNode item in list)
			{
				if (item == serverNode)
				{
					return;
				}
			}
			list.Add(serverNode);
		}
		else
		{
			List<CVBaseServerNode> list2 = new List<CVBaseServerNode>();
			list2.Add(serverNode);
			topicServer.Add(topic, list2);
		}
	}

	public void DoSubscribe(string topic, CVServiceProxy serverNodeProxy)
	{
		if (topicServerProxy.ContainsKey(topic))
		{
			topicServerProxy[topic].Add(serverNodeProxy);
			return;
		}
		List<CVServiceProxy> list = new List<CVServiceProxy>();
		list.Add(serverNodeProxy);
		topicServerProxy.Add(topic, list);
	}

	public void Start(string serialNumber)
	{
		Running = true;
		CVStartCFC start = new CVStartCFC(serialNumber);
		DoDispatch(start);
	}

	protected void DoDispatch(CVStartCFC start)
	{
		if (start.GetActionType() == ActionTypeEnum.Start)
		{
			start.SetStartNode(this);
			logger.InfoFormat("===============开始运行流程文件[{0}/{1}]", m_nodeName, start.SerialNumber);
		}
		DoStartTransferData(start);
	}

	public void Stop(string serialNumber)
	{
		CVStartCFC action = new CVStartCFC(ActionTypeEnum.Stop, serialNumber);
		DoStartTransferData(action);
	}

	public void StopAll()
	{
		foreach (KeyValuePair<string, CVStartCFC> startAction in startActions)
		{
			CVStartCFC action = new CVStartCFC(ActionTypeEnum.Stop, startAction.Key);
			DoStartTransferData(action);
		}
	}

	public virtual void Dispose()
	{
		topicServer.Clear();
	}

	public void FireFinished(CVStartCFC startAction)
	{
		StatusTypeEnum flowStatus = startAction.FlowStatus;
		string message = string.Empty;
		if (flowStatus == StatusTypeEnum.Failed && startAction.Data.ContainsKey("Msg"))
		{
			message = startAction.Data["Msg"].ToString();
		}
		logger.InfoFormat("Fire Flow Finished Before");
		this.Finished?.Invoke(this, new FlowStartEventArgs(startAction.SerialNumber, flowStatus, (long)startAction.GetTotalTime().TotalMilliseconds, message));
		logger.InfoFormat("Fire Flow Finished End");
	}
}
