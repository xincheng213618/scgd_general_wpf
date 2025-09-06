using System.Collections.Generic;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.MQTT;

[STNode("/10 MQTT")]
public class MQTTPublishHub : STNodeInHub
{
	private string _Server;

	private int _Port = 1883;

	private MQTTHelper _MQTTHelper;

	private Dictionary<string, MQTTObject> MQTTEvents = new Dictionary<string, MQTTObject>();

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

	public MQTTPublishHub()
		: base("MQTT发布HUB")
	{
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		string userName = "";
		string password = "";
		MQTTHelper.GetDefaultCfg(ref _Server, ref _Port, ref userName, ref password);
	}

	protected override void DoInputConnected(STNodeOption sender, STNodeOptionEventArgs e)
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

	protected override void DoInputDataTransfer(STNodeOption sender, STNodeOptionEventArgs e)
	{
		if (e.Status == ConnectionStatus.Connected && e.TargetOption.Data != null)
		{
			MQActionEvent mQActionEvent = (MQActionEvent)e.TargetOption.Data;
			if (mQActionEvent != null && mQActionEvent.Topic != null && mQActionEvent.Topic.Length > 0 && mQActionEvent.Message.Length > 0)
			{
				_MQTTHelper.PublishAsync_Client(mQActionEvent.Topic, mQActionEvent.Message, retained: false);
			}
		}
	}

	private void onMsgSub(ResultData_MQTT resultData_MQTT)
	{
	}
}
