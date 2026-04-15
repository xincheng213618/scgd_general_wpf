using System.Collections.Generic;
using System.Drawing;
using FlowEngineLib.Base;
using FlowEngineLib.MQTT;
using log4net;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Start;

[STNode("/00 全局")]
public class MQTTStartNode : BaseStartNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(MQTTStartNode));

	private string _Server;

	private int _Port = 1883;

	private string _StartTopic;

	private string _StatusTopicName;

	private MQTTHelper _MQTTHelper;

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

	public MQTTStartNode()
		: base("Start_MQTT")
	{
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		_StartTopic = "FLOW/CMD";
		_StatusTopicName = "FLOW/STATUS";
		base.TitleColor = Color.FromArgb(200, Color.Goldenrod);
	}

	protected override void OnNodeNameChanged(string oldValue, string newValue)
	{
		if (_MQTTHelper != null && _MQTTHelper.IsClientConnect())
		{
			_MQTTHelper.UnsubscribeAsync_Client(GetStartTopic(oldValue));
			_MQTTHelper.SubscribeAsync_Client(GetStartTopic());
		}
	}

	protected override void DoStartConnected(STNodeOption sender, STNodeOptionEventArgs e)
	{
		if (_MQTTHelper == null)
		{
			_MQTTHelper = new MQTTHelper();
			string userName = "";
			string password = "";
			MQTTHelper.GetDefaultCfg(ref _Server, ref _Port, ref userName, ref password);
			_MQTTHelper.CreateMQTTClientAndStart(_Server, _Port, userName, password, onMsgSub);
			logger.Info("Begin Connect MQTT");
		}
	}

	protected override void DoStartDisConnected(STNodeOption sender, STNodeOptionEventArgs e)
	{
		MQTTDisConnected();
	}

	private void MQTTDisConnected()
	{
		if (_MQTTHelper != null)
		{
			_MQTTHelper.DisconnectAsync_Client();
			_MQTTHelper = null;
			logger.Info("Begin Disconnect MQTT");
		}
	}

	private string GetStartTopic(string nodeName)
	{
		return _StartTopic + "/" + nodeName;
	}

	private string GetStartTopic()
	{
		return GetStartTopic(m_nodeName);
	}

	private string GetStatusTopic()
	{
		return _StatusTopicName + "/" + m_nodeName;
	}

	private void onMsgSub(ResultData_MQTT resultData_MQTT)
	{
		string value = (string)resultData_MQTT.ResultObject2;
		string text = (string)resultData_MQTT.ResultObject1;
		if (resultData_MQTT.EventType == EventTypeEnum.MsgRecv)
		{
			CVMQTTRequest cVMQTTRequest = null;
			if (!string.IsNullOrEmpty(value))
			{
				cVMQTTRequest = JsonConvert.DeserializeObject<CVMQTTRequest>(value);
			}
			if (string.IsNullOrEmpty(text) || cVMQTTRequest == null)
			{
				return;
			}
			if (topicServer.ContainsKey(text))
			{
				List<CVBaseServerNode> list = new List<CVBaseServerNode>(topicServer[text]);
				CVBaseDataFlowResp statusEvent = JsonConvert.DeserializeObject<CVBaseDataFlowResp>(value);
				using List<CVBaseServerNode>.Enumerator enumerator = list.GetEnumerator();
				while (enumerator.MoveNext() && !enumerator.Current.DoServerStatusRecv(statusEvent))
				{
				}
				return;
			}
			if (text.Equals(GetStartTopic()))
			{
				CVStartCFC action = GetAction(cVMQTTRequest);
				if (action != null)
				{
					DoDispatch(action);
				}
			}
		}
		else if (resultData_MQTT.EventType == EventTypeEnum.ClientConnected)
		{
			if (_MQTTHelper != null && _MQTTHelper.IsClientConnect())
			{
				_MQTTHelper.SubscribeAsync_Client(GetStartTopic());
				base.Ready = true;
				logger.Debug("MQTT Connected");
			}
		}
		else if (resultData_MQTT.EventType == EventTypeEnum.ClientDisconnected)
		{
			base.Ready = false;
			logger.Debug("MQTT DisConnected");
		}
	}

	private CVStartCFC GetAction(CVMQTTRequest evt)
	{
		CVStartCFC result = null;
		string value = evt.EventName.ToUpper();
		if ("START".Equals(value))
		{
			result = new CVStartCFC(evt.SerialNumber);
		}
		else if ("STOP".Equals(value))
		{
			result = new CVStartCFC(ActionTypeEnum.Stop, evt.SerialNumber);
		}
		else if ("PAUSE".Equals(value))
		{
			result = new CVStartCFC(ActionTypeEnum.Pause, evt.SerialNumber);
		}
		else if ("FAIL".Equals(value))
		{
			result = new CVStartCFC(ActionTypeEnum.Fail, evt.SerialNumber);
		}
		return result;
	}

	public override void DoPublishStatus(string msg)
	{
		if (_MQTTHelper != null && _MQTTHelper.IsClientConnect())
		{
			_MQTTHelper.PublishAsync_Client(GetStatusTopic(), msg, retained: false);
		}
	}

	public override void DoPublish(MQActionEvent act)
	{
		if (_MQTTHelper != null && _MQTTHelper.IsClientConnect())
		{
			_MQTTHelper.PublishAsync_Client(act.Topic, act.Message, retained: false);
		}
	}

	public override void DoSubscribe(string topic, CVBaseServerNode serverNode)
	{
		if (!topicServer.ContainsKey(topic) && _MQTTHelper != null && _MQTTHelper.IsClientConnect())
		{
			_MQTTHelper.SubscribeAsync_Client(topic);
		}
		base.DoSubscribe(topic, serverNode);
	}

	public override void Dispose()
	{
		base.Dispose();
		MQTTDisConnected();
	}
}
