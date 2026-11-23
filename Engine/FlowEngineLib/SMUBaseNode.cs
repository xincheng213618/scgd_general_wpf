#pragma warning disable CA1708
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using FlowEngineLib.Base;
using FlowEngineLib.MQTT;
using FlowEngineLib.Node.SMU;
using FlowEngineLib.SMU;
using log4net;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

public class SMUBaseNode : CVBaseServerNode, ICVLoopNextNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(SMUBaseNode));
	private static readonly object OutputOwnership = new();
	private static readonly ConditionalWeakTable<object, List<WeakReference<RemoteOutput>>> RemoteOutputs = new();
	private readonly ConditionalWeakTable<CVTransAction, RunOutputs> runOutputs = new();

	private sealed record RemoteTarget(string Topic, string Service, string Device);
	private sealed record RemoteOutput(
		RemoteTarget Target, string MessageId, string SerialNumber, string Token, int ZIndex,
		Action<MQActionEvent, Func<bool>> Publish)
	{
		public SMUChannelType? Channel { get; set; }
		public bool IsCurrent { get; set; } = true;
	}

	private sealed class RunOutputs
	{
		public bool Released { get; set; }
		public List<RemoteOutput> Remote { get; } = new();
		public Dictionary<(object Session, SMUChannelType Channel), Action> Local { get; } = new();
	}

	protected STNodeOption m_in_next;

	protected STNodeEditText<string> m_ctrl_curValue;

	protected STNodeEditText<string> m_ctrl_lpName;

	protected STNodeEditText<string> m_ctrl_channel;

	protected STNodeEditText<bool> m_ctrl_closeOut;

	protected string loopName;

	protected SMUChannelType _channel;

	protected SourceType _source;

	protected bool _IsCloseOutput;

	protected bool _IsAutoRng;

	protected double _SrcRng;

	protected double _LmtRng;

	protected float m_begin_val;

	protected float m_end_val;

	protected int m_point_num;

	protected double m_cur_src_val;

	protected float _limitVal;

	protected double m_step_val;

	protected int m_step_idx;

	public bool IsStarted;

	protected List<SMUSrcValueData> srcValues;

	[STNodeProperty("LoopName", "LoopName", false, true)]
	public string LoopName
	{
		get
		{
			return loopName;
		}
		set
		{
			loopName = value;
			m_ctrl_lpName.Value = loopName;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("关闭输出", "关闭输出", true)]
	public bool IsCloseOutput
	{
		get
		{
			return _IsCloseOutput;
		}
		set
		{
			_IsCloseOutput = value;
			OnPropertyChanged();
		}
	}

	protected string DisTypeString => $"{_channel.ToString()}/{_source.ToString()}";

	public SMUBaseNode(string title, string nodeType, string nodeName, string deviceCode)
		: base(title, nodeType, nodeName, deviceCode)
	{
		m_is_out_release = false;
		m_has_svr_item = false;
		_IsCloseOutput = false;
		IsStarted = false;
		_IsAutoRng = true;
		operatorCode = "GetData";
		srcValues = null;
		_source = SourceType.Voltage_V;
		m_begin_val = 0f;
		m_end_val = 5f;
		_limitVal = 1f;
		m_point_num = 5;
		m_step_idx = 0;
		loopName = "SMULoop";
		base.Height += 95;
	}

	protected void CreateSMUNextControl()
	{
		m_in_next = base.InputOptions.Add("IN_LP_NEXT", typeof(CVLoopCFC), bSingle: true);
		base.InputOptions.Add(STNodeOption.Empty);
		base.InputOptions.Add(STNodeOption.Empty);
		m_in_next.DataTransfer += m_in_next_DataTransfer;
	}

	protected void CreateSMUControl(int y = 50)
	{
		CreateSMUNextControl();
		Rectangle custom_item = m_custom_item;
		custom_item.Y = y;
		m_ctrl_channel = CreateStringControl(custom_item, "类型:", DisTypeString);
		custom_item.Y += 25;
		m_ctrl_curValue = CreateStringControl(custom_item, "当前值:", string.Empty);
		custom_item.Y += 25;
		m_ctrl_lpName = CreateStringControl(custom_item, "LoopName:", loopName);
		custom_item.Y += 25;
		m_ctrl_closeOut = CreateControl(typeof(STNodeEditText<bool>), custom_item, "关闭输出:", _IsCloseOutput);
	}

	protected virtual void updateUI()
	{
		m_ctrl_channel.Value = DisTypeString;
		if (m_step_idx == 0)
		{
			m_ctrl_curValue.Value = $"{m_begin_val}-{m_end_val}/{m_point_num}";
		}
		else
		{
			m_ctrl_curValue.Value = string.Format("{2:F4}:{0}/{1}", m_step_idx, m_point_num, m_cur_src_val);
		}
	}

	protected virtual void end(CVTransAction trans)
	{
		updateUI();
	}

	protected override void DispatchRemoteCommand(CVTransAction trans, MQActionEvent action, CVBaseEventCmd command)
	{
		var start = trans.trans_action.GetStartNode();
		RemoteFlowResultTracking.TrackRegistered(trans.trans_action, start, action, command, RemoteResultBinderOverride);
		CVMQTTRequest request = command.cmd;
		var target = new RemoteTarget(action.Topic, request.ServiceCode, request.DeviceCode);
		SMUChannelType? channel = (request.Data as SMUData)?.Channel;
		bool closeOutput = _IsCloseOutput;
		start.PublishWithDeferredCleanup(action, (publisher, publish) =>
		{
			lock (OutputOwnership)
			{
				RunOutputs run = runOutputs.GetValue(trans, static _ => new());
				if (run.Released || !OwnsCommand(trans, command))
					return false;

				RetireRemoteOutputs(publisher, target, channel);
				run.Remote.RemoveAll(output => !output.IsCurrent);
				if (closeOutput)
				{
					var output = new RemoteOutput(target, request.MsgID, request.SerialNumber, request.Token, request.ZIndex, publish)
					{
						Channel = channel
					};
					run.Remote.Add(output);
					RemoteOutputs.GetValue(publisher, static _ => new()).Add(new(output));
				}
				return true;
			}
		});
	}

	internal static void DispatchRemoteSweep(CVTransAction trans, MQActionEvent action, CVBaseEventCmd command, RemoteFlowResultBinder binder)
	{
		var start = trans.trans_action.GetStartNode();
		RemoteFlowResultTracking.TrackRegistered(trans.trans_action, start, action, command, binder);
		var target = new RemoteTarget(action.Topic, command.cmd.ServiceCode, command.cmd.DeviceCode);
		var channel = (command.cmd.Data as SMUSweepParam)?.DeviceParam?.Channel;
		start.PublishWithDeferredCleanup(action, (publisher, _) =>
		{
			lock (OutputOwnership)
			{
				if (!OwnsCommand(trans, command))
					return false;
				RetireRemoteOutputs(publisher, target, channel);
				return true;
			}
		});
	}

	private static bool OwnsCommand(CVTransAction trans, CVBaseEventCmd command) =>
		!trans.IsCanceled && !trans.trans_action.RuntimeResources.IsDisposed
		&& trans.TryGetActionCommand(command.cmd.MsgID, out CVBaseEventCmd pending) && ReferenceEquals(command, pending);

	private static void RetireRemoteOutputs(object publisher, RemoteTarget target, SMUChannelType? channel)
	{
		if (!RemoteOutputs.TryGetValue(publisher, out List<WeakReference<RemoteOutput>> outputs))
			return;

		// A template command can use either channel until its correlated reply identifies the output.
		outputs.RemoveAll(reference =>
		{
			if (!reference.TryGetTarget(out RemoteOutput previous) || !previous.IsCurrent)
				return true;
			if (previous.Target != target || (channel.HasValue && previous.Channel.HasValue && channel != previous.Channel))
				return false;
			previous.IsCurrent = false;
			return true;
		});
	}

	protected override void ExecuteLocalCommand(CVTransAction trans, FlowLocalExecution execution)
	{
		bool closeOutput = _IsCloseOutput;
		base.ExecuteLocalCommand(trans, execution);
		if (!closeOutput || execution is not ISmuFlowOutput source || source.CaptureCloseOutput() is not { } output)
			return;

		lock (OutputOwnership)
		{
			RunOutputs run = runOutputs.GetValue(trans, static _ => new());
			if (!run.Released && !trans.IsCanceled && !trans.trans_action.RuntimeResources.IsDisposed)
				run.Local[(output.Session, output.Channel)] = output.Close;
		}
	}

	protected void RecordTemplateOutputChannel(CVStartCFC action, string messageId, SMUChannelType channel)
	{
		CVTransAction trans = null;
		if (!Enum.IsDefined(channel) || !HasTransAction(action.SerialNumber, ref trans)
			|| !ReferenceEquals(action.RuntimeResources, trans.trans_action.RuntimeResources))
			return;

		lock (OutputOwnership)
		{
			if (runOutputs.TryGetValue(trans, out RunOutputs run) && !run.Released)
			{
				RemoteOutput output = run.Remote.Find(item => item.IsCurrent && item.MessageId == messageId);
				if (output != null)
					output.Channel = channel;
			}
		}
	}

	private void m_in_next_DataTransfer(object sender, STNodeOptionEventArgs e)
	{
		if (e.TargetOption.Data == null)
		{
			return;
		}
		CVLoopCFC cVLoopCFC = (CVLoopCFC)e.TargetOption.Data;
		CVTransAction trans = null;
		if (HasTransAction(cVLoopCFC.SerialNumber, ref trans) && trans.trans_action.IsRunning)
		{
			if (HasNext())
			{
				m_cur_src_val = BuildNextSrcValue();
				DoNextActionEvent(trans);
				m_step_idx++;
				updateUI();
				AddCFCData(trans.trans_action);
			}
			else
			{
				end(trans);
			}
		}
	}

	protected virtual double BuildNextSrcValue()
	{
		return m_cur_src_val + m_step_val;
	}

	protected void AddIVData(CVServerResponse resp, CVStartCFC start)
	{
		double v = resp.Data.V;
		double i = resp.Data.I;
		int masterId = resp.Data.MasterId;
		int masterResultType = resp.Data.MasterResultType;
		SMUResultData value = new SMUResultData(_channel, v, i, masterId, masterResultType);
		string key = "SMUResult";
		start.Data[key] = value;
	}

	protected void AddCFCData(CVStartCFC start)
	{
		string key = loopName;
		LoopDataInfo loopDataInfo = null;
		if (start.Data.ContainsKey(key))
		{
			loopDataInfo = start.Data[key] as LoopDataInfo;
		}
		else
		{
			loopDataInfo = new LoopDataInfo();
			start.Data.Add(key, loopDataInfo);
		}
		loopDataInfo.Step = m_step_idx;
		loopDataInfo.HasNext = HasNext();
	}

	private MQActionEvent DoNextActionEvent(CVTransAction trans)
	{
		CVStartCFC trans_action = trans.trans_action;
		string token = GetToken();
		CVMQTTRequest cVMQTTRequest = new CVMQTTRequest(GetServiceName(), m_deviceCode, operatorCode, trans_action.SerialNumber, new SMUData(_channel, _source == SourceType.Voltage_V, m_cur_src_val, _limitVal, _IsAutoRng, _SrcRng, _LmtRng), token, base.ZIndex);
		CVBaseEventCmd cmd = AddActionCmd(trans, cVMQTTRequest);
		if (cmd == null)
		{
			return null;
		}
		string message = JsonConvert.SerializeObject(cVMQTTRequest, Formatting.None);
		MQActionEvent mQActionEvent = new MQActionEvent(cVMQTTRequest.MsgID, m_nodeName, m_deviceCode, GetSendTopic(), cVMQTTRequest.EventName, message, token);
		DoTransferToServer(trans, mQActionEvent, cmd);
		if (logger.IsDebugEnabled)
		{
			logger.DebugFormat("[{0}] Next Step _source value = {1}", ToShortString(), m_cur_src_val);
		}
		return mQActionEvent;
	}

	protected override void DoTransCompleted(CVTransAction trans, CVStartCFC action)
	{
		base.DoTransCompleted(trans, action);
		end(trans);
	}

	protected bool HasNext()
	{
		if (logger.IsDebugEnabled)
		{
			logger.DebugFormat("[{0}] HasNext Step = {1}/{2}", ToShortString(), m_step_idx, m_point_num);
		}
		return m_step_idx < m_point_num;
	}

	protected override void OnServerResponse(CVServerResponse resp, CVStartCFC startCFC)
	{
		base.OnServerResponse(resp, startCFC);
		if (resp != null && resp.Status == ActionStatusEnum.Finish)
		{
			AddIVData(resp, startCFC);
		}
	}

	protected override CVMQTTRequest getActionEvent(STNodeOptionEventArgs e)
	{
		CVMQTTRequest result = null;
		CVStartCFC cVStartCFC = (CVStartCFC)e.TargetOption.Data;
		if (cVStartCFC.IsRunning)
		{
			if (BuildValueData())
			{
				m_op_end.TransferData(null);
				m_step_idx = 0;
				result = new CVMQTTRequest(GetServiceName(), GetDeviceCode(), operatorCode, cVStartCFC.SerialNumber, new SMUData(_channel, _source == SourceType.Voltage_V, m_cur_src_val, _limitVal, _IsAutoRng, _SrcRng, _LmtRng), GetToken(), base.ZIndex);
				IsStarted = true;
				m_step_idx++;
				updateUI();
				AddCFCData(cVStartCFC);
			}
			else if (logger.IsErrorEnabled)
			{
				logger.ErrorFormat("[{0}]Build SMU Value data failed", ToShortString());
			}
		}
		else
		{
			m_cur_src_val = 0.0;
			m_step_val = 0.0;
		}
		return result;
	}

	protected virtual bool BuildValueData()
	{
		double num = m_end_val - m_begin_val;
		if (m_point_num > 1)
		{
			m_step_val = num / (double)(m_point_num - 1);
		}
		else
		{
			m_step_val = num;
		}
		m_cur_src_val = m_begin_val;
		return true;
	}

	protected override void Reset(CVStartCFC action)
	{
		IsStarted = false;
	}

	protected override void Reset(CVTransAction trans)
	{
		base.Reset(trans);
		if (trans == null)
			return;

		RunOutputs run;
		lock (OutputOwnership)
		{
			run = runOutputs.GetValue(trans, static _ => new());
			if (run.Released)
				return;
			run.Released = true;
		}

		foreach (Action close in run.Local.Values)
		{
			try { close(); }
			catch (Exception ex) { logger.Error("本地源表关闭输出失败", ex); }
		}
		foreach (RemoteOutput output in run.Remote)
		{
			if (trans.trans_action.FlowStatus == StatusTypeEnum.OverTime || trans.IsCanceled)
				logger.WarnFormat("源表采集结束时设备输出状态待确认，将尝试在原路由关闭输出。Device={0}, SN={1}",
					output.Target.Device, output.SerialNumber);
			ObserveCloseOutputTask(CloseRemoteOutputAsync(output));
		}
	}

	protected virtual Task WaitForCloseOutputDelayAsync() => Task.Delay(500);

	protected virtual void ObserveCloseOutputTask(Task task)
	{
		_ = task.ContinueWith(failed => logger.Error("服务源表关闭输出失败", failed.Exception),
			TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
	}

	private async Task CloseRemoteOutputAsync(RemoteOutput output)
	{
		SMUChannelType channel;
		lock (OutputOwnership)
		{
			if (!output.IsCurrent)
				return;
			if (!output.Channel.HasValue)
			{
				output.IsCurrent = false;
				logger.WarnFormat("源表 {0} 未确认模板输出通道，无法发送关闭输出命令。SN={1}", output.Target.Device, output.SerialNumber);
				return;
			}
			channel = output.Channel.Value;
		}

		await WaitForCloseOutputDelayAsync().ConfigureAwait(false);
		var request = new CVMQTTRequest(output.Target.Service, output.Target.Device, "CloseOutput", output.SerialNumber,
			new SMUCloseOutputRequestParam { Channel = channel }, output.Token, output.ZIndex);
		var action = new MQActionEvent(request.MsgID, output.Target.Service, output.Target.Device, output.Target.Topic,
			request.EventName, JsonConvert.SerializeObject(request, Formatting.None), output.Token);
		output.Publish(action, () =>
		{
			lock (OutputOwnership)
			{
				if (!output.IsCurrent)
					return false;
				output.IsCurrent = false;
				return true;
			}
		});
	}
}
