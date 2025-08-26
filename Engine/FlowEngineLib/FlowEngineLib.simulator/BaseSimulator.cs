using System.Timers;
using FlowEngineLib.Base;
using FlowEngineLib.MQTT;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.simulator;

internal class BaseSimulator : STNode
{
	private string _Server;

	private int _Port = 1883;

	private int _Time = 2000;

	private MQTTHelper _MQTTHelper;

	private STNodeOption m_in;

	protected CVMQTTRequest finish;

	protected MQActionEvent mqAction;

	protected string nodeCode;

	protected string deviceCode;

	protected string serverRespEventName;

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

	protected BaseSimulator(string title, string nodeCode, string deviceCode)
	{
		base.Title = title;
		this.nodeCode = nodeCode;
		this.deviceCode = deviceCode;
		base.AutoSize = true;
		serverRespEventName = "Finish";
	}

	protected override void OnCreate()
	{
		m_in = new STNodeOption("IN", typeof(MQActionEvent), bSingle: true);
		base.InputOptions.Add(m_in);
		m_in.Connected += m_in_Connected;
		m_in.DataTransfer += m_in_DataTransfer;
	}

	private void SetTimer(double interval)
	{
		if (interval > 0.0)
		{
			System.Timers.Timer timer = new System.Timers.Timer(interval);
			timer.Elapsed += DoPublishFinish;
			timer.AutoReset = true;
			timer.Enabled = true;
		}
		else if (finish != null)
		{
			_MQTTHelper.PublishAsync_Client(mqAction.Topic, JsonConvert.SerializeObject(finish), retained: false);
		}
	}

	private void DoPublishFinish(object source, ElapsedEventArgs e)
	{
		System.Timers.Timer obj = source as System.Timers.Timer;
		obj.Stop();
		obj.Dispose();
		if (finish != null)
		{
			_MQTTHelper.PublishAsync_Client(mqAction.Topic, JsonConvert.SerializeObject(finish), retained: false);
		}
	}

	private void m_in_DataTransfer(object sender, STNodeOptionEventArgs e)
	{
		if (e.Status != ConnectionStatus.Connected)
		{
			return;
		}
		if (e.TargetOption.Data != null)
		{
			MQActionEvent mQActionEvent = (MQActionEvent)e.TargetOption.Data;
			if (mQActionEvent != null)
			{
				SetOptionText(m_in, mQActionEvent.Message);
				finish = GetResponseEvent(mQActionEvent);
				if (finish != null)
				{
					mqAction = mQActionEvent;
					SetTimer(_Time);
				}
			}
		}
		else
		{
			SetOptionText(m_in, "--");
		}
	}

	protected virtual CVMQTTRequest GetResponseEvent(MQActionEvent msg)
	{
		CVMQTTRequest cVMQTTRequest = JsonConvert.DeserializeObject<CVMQTTRequest>(msg.Message);
		CVServerResponse data = new CVServerResponse(cVMQTTRequest.MsgID, ActionStatusEnum.Finish, "", cVMQTTRequest.EventName, cVMQTTRequest.Data);
		return new CVMQTTRequest(nodeCode, deviceCode, serverRespEventName, cVMQTTRequest.SerialNumber, data, string.Empty);
	}

	private void m_in_Connected(object sender, STNodeOptionEventArgs e)
	{
		SetOptionText(m_in, "--");
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
