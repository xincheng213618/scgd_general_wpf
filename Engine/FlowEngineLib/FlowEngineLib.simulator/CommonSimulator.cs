using System.Collections.Generic;
using System.Timers;
using FlowEngineLib.Base;
using FlowEngineLib.MQTT;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.simulator;

internal class CommonSimulator : STNodeInHub
{
	private string _Server;

	private int _Port = 1883;

	private int _Time = 2000;

	private MQTTHelper _MQTTHelper;

	private Dictionary<System.Timers.Timer, MQActionEvent> finishDic;

	private string serverRespEventName;

	[STNodeProperty("Server", "Server")]
	public string Server
	{
		get
		{
			return _Server;
		}
		set
		{
			_Server = value;
		}
	}

	[STNodeProperty("Port", "Port")]
	public int Port
	{
		get
		{
			return _Port;
		}
		set
		{
			_Port = value;
		}
	}

	[STNodeProperty("Time", "Time")]
	public int Time
	{
		get
		{
			return _Time;
		}
		set
		{
			_Time = value;
		}
	}

	public CommonSimulator()
		: base(bSingle: true)
	{
		base.Title = "通用模拟器";
		serverRespEventName = "Finish";
		finishDic = new Dictionary<System.Timers.Timer, MQActionEvent>();
	}

	protected override void DoInputDataTransfer(STNodeOption sender, STNodeOptionEventArgs e)
	{
		if (e.Status != ConnectionStatus.Connected)
		{
			return;
		}
		if (e.TargetOption.Data != null && e.TargetOption.Data.GetType() == typeof(MQActionEvent))
		{
			MQActionEvent mQActionEvent = (MQActionEvent)e.TargetOption.Data;
			if (mQActionEvent != null)
			{
				SetOptionText(sender, mQActionEvent.Message);
				AddInData(mQActionEvent, _Time);
			}
		}
		else
		{
			SetOptionText(sender, "--");
		}
	}

	private void AddInData(MQActionEvent msg, int interval)
	{
		if (interval > 0)
		{
			System.Timers.Timer timer = new System.Timers.Timer(interval);
			timer.Elapsed += DoPublishFinish;
			timer.AutoReset = true;
			timer.Enabled = true;
			finishDic.Add(timer, msg);
		}
	}

	private void DoPublishFinish(object source, ElapsedEventArgs e)
	{
		System.Timers.Timer timer = source as System.Timers.Timer;
		timer.Stop();
		timer.Dispose();
		if (finishDic.ContainsKey(timer))
		{
			MQActionEvent mQActionEvent = finishDic[timer];
			CVMQTTRequest responseEvent = GetResponseEvent(mQActionEvent);
			if (_MQTTHelper != null && _MQTTHelper.IsClientConnect())
			{
				_MQTTHelper.PublishAsync_Client(mQActionEvent.Topic, JsonConvert.SerializeObject(responseEvent), retained: false);
			}
		}
	}

	protected virtual CVMQTTRequest GetResponseEvent(MQActionEvent msg)
	{
		CVMQTTRequest cVMQTTRequest = JsonConvert.DeserializeObject<CVMQTTRequest>(msg.Message);
		CVServerResponse data = new CVServerResponse(cVMQTTRequest.MsgID, ActionStatusEnum.Finish, "", cVMQTTRequest.EventName, cVMQTTRequest.Data);
		return new CVMQTTRequest(msg.ServiceCode, msg.DeviceCode, serverRespEventName, cVMQTTRequest.SerialNumber, data, string.Empty);
	}

	protected override void DoInputConnected(STNodeOption sender, STNodeOptionEventArgs e)
	{
		CreateMQTTClientAndStart();
	}

	private void CreateMQTTClientAndStart()
	{
		if (_MQTTHelper == null)
		{
			_MQTTHelper = new MQTTHelper();
			string userName = "";
			string password = "";
			MQTTHelper.GetDefaultCfg(ref _Server, ref _Port, ref userName, ref password);
			_MQTTHelper.CreateMQTTClientAndStart(_Server, _Port, userName, password, onMsgSub);
		}
	}

	private void onMsgSub(ResultData_MQTT resultData_MQTT)
	{
	}
}
