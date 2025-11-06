using FlowEngineLib.MQTT;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

[STNode("/10 MQTT")]
internal class MQTTCustomSubscribeNode : MQTTBaseNode
{
	private string _Topic;

	private STNodeOption m_op_sub;

	[STNodeProperty("Topic", "Topic")]
	public string Topic
	{
		get
		{
			return _Topic;
		}
		set
		{
			_Topic = value;
		}
	}

	public MQTTCustomSubscribeNode()
		: base("MQTT自定义订阅")
	{
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_op_sub = new STNodeOption("", typeof(bool), bSingle: false);
		base.OutputOptions.Add(m_op_sub);
		m_op_sub.Connected += m_op_sub_Connected;
	}

	private void m_op_sub_Connected(object sender, STNodeOptionEventArgs e)
	{
		if (_MQTTHelper == null)
		{
			_MQTTHelper = new MQTTHelper();
			string userName = "";
			string password = "";
			MQTTHelper.GetDefaultCfg(ref _Server, ref _Port, ref userName, ref password);
			_MQTTHelper.CreateMQTTClientAndStart(_Server, _Port, userName, password, onMsgSub);
		}
		else if (_MQTTHelper.IsClientConnect())
		{
			_MQTTHelper.SubscribeAsync_Client(_Topic);
		}
	}

	private void onMsgSub(ResultData_MQTT resultData_MQTT)
	{
		switch (resultData_MQTT.EventType)
		{
		case EventTypeEnum.MsgRecv:
			DoRecvMsg(resultData_MQTT);
			break;
		case EventTypeEnum.ClientConnected:
			_MQTTHelper.SubscribeAsync_Client(_Topic);
			break;
		}
		LogHelper.WriteLog(JsonConvert.SerializeObject((object)resultData_MQTT, (Formatting)0));
	}

	private void DoRecvMsg(ResultData_MQTT resultData_MQTT)
	{
		if (resultData_MQTT.ResultCode == 1)
		{
			string value = resultData_MQTT.ResultObject1.ToString();
			if (!string.IsNullOrEmpty(value) && _Topic.Equals(value))
			{
				m_op_sub.TransferData(true);
			}
		}
	}
}
