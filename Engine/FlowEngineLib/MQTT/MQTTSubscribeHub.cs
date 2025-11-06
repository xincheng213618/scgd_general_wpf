using System;
using System.Collections.Generic;
using FlowEngineLib.Base;
using log4net;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.MQTT;

[STNode("/10 MQTT")]
public class MQTTSubscribeHub : STNodeOutHub
{
	public static readonly ILog logger = LogManager.GetLogger(typeof(MQTTSubscribeHub));

	private string _Server;

	private int _Port = 1883;

	private MQTTHelper _MQTTHelper;

	private Dictionary<string, MQTTObjectTopic> MQTTEvents = new Dictionary<string, MQTTObjectTopic>();

	private static readonly object _lock = new object();

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

	public MQTTSubscribeHub()
		: base("MQTT订阅HUB")
	{
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		string userName = "";
		string password = "";
		MQTTHelper.GetDefaultCfg(ref _Server, ref _Port, ref userName, ref password);
	}

	protected override void Addhub()
	{
		base.Addhub();
	}

	public void SetEventInfo(STNodeOption op, string eventName, string topic, string serviceCode, string deviceCode)
	{
		MQTTObject mQTT = new MQTTObject(new MQActionEvent(Guid.NewGuid().ToString(), serviceCode, deviceCode, topic, eventName, string.Empty, string.Empty), op);
		MQTTObjectTopic mQTTObjectTopic = addMQTTEvent(topic, mQTT);
		if (_MQTTHelper != null && _MQTTHelper.IsClientConnect() && !mQTTObjectTopic.MQTTSubscribed)
		{
			_MQTTHelper.SubscribeAsync_Client(mQTTObjectTopic.TopicName);
		}
	}

	protected override void DoOutputConnected(STNodeOption sender, STNodeOptionEventArgs e)
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

	protected override void DoOutputDisConnected(STNodeOption sender, STNodeOptionEventArgs e)
	{
		if (_MQTTHelper != null && _MQTTHelper.IsClientConnect())
		{
			MQTTObjectTopic mQTTObjectTopic = removeMQTTEvent(sender);
			if (mQTTObjectTopic != null && mQTTObjectTopic.MQTTObjects != null && mQTTObjectTopic.MQTTObjects.Count == 0)
			{
				_MQTTHelper.UnsubscribeAsync_Client(mQTTObjectTopic.TopicName);
			}
		}
	}

	private MQTTObjectTopic addMQTTEvent(string topic, MQTTObject mQTT)
	{
		MQTTObjectTopic mQTTObjectTopic;
		lock (_lock)
		{
			if (MQTTEvents.ContainsKey(topic))
			{
				mQTTObjectTopic = MQTTEvents[topic];
			}
			else
			{
				mQTTObjectTopic = new MQTTObjectTopic(topic);
				MQTTEvents.Add(topic, mQTTObjectTopic);
			}
		}
		mQTTObjectTopic.AddMQTTEvent(mQTT);
		return mQTTObjectTopic;
	}

	private MQTTObjectTopic removeMQTTEvent(STNodeOption op)
	{
		foreach (MQTTObjectTopic mQTTEvent in getMQTTEventList())
		{
			if (mQTTEvent.removeMQTTEvent(op))
			{
				return mQTTEvent;
			}
		}
		return null;
	}

	private MQTTObjectTopic removeMQTTEvent(string topic)
	{
		MQTTObjectTopic result = null;
		lock (_lock)
		{
			if (MQTTEvents.ContainsKey(topic))
			{
				result = MQTTEvents[topic];
				MQTTEvents.Remove(topic);
			}
		}
		return result;
	}

	private MQTTObjectTopic getMQTTEvent(STNodeOption op)
	{
		foreach (MQTTObjectTopic mQTTEvent in getMQTTEventList())
		{
			if (mQTTEvent.hasMQTTEvent(op))
			{
				return mQTTEvent;
			}
		}
		return null;
	}

	private MQTTObjectTopic getMQTTEvent(string topic)
	{
		MQTTObjectTopic result = null;
		lock (_lock)
		{
			if (MQTTEvents.ContainsKey(topic))
			{
				result = MQTTEvents[topic];
			}
		}
		return result;
	}

	private List<MQTTObjectTopic> getMQTTEventList()
	{
		lock (_lock)
		{
			return new List<MQTTObjectTopic>(MQTTEvents.Values);
		}
	}

	private void onMsgSub(ResultData_MQTT resultData_MQTT)
	{
		logger.Debug((object)JsonConvert.SerializeObject((object)resultData_MQTT, (Formatting)0));
		string text = (string)resultData_MQTT.ResultObject2;
		string text2 = (string)resultData_MQTT.ResultObject1;
		if (resultData_MQTT.EventType == EventTypeEnum.Subscribe)
		{
			if (MQTTEvents.ContainsKey(text2))
			{
				MQTTEvents[text2].MQTTSubscribed = resultData_MQTT.ResultCode == 1;
			}
			return;
		}
		if (resultData_MQTT.EventType == EventTypeEnum.Unsubscribe)
		{
			removeMQTTEvent(text2);
			return;
		}
		if (resultData_MQTT.EventType == EventTypeEnum.ClientConnected)
		{
			foreach (MQTTObjectTopic mQTTEvent3 in getMQTTEventList())
			{
				if (!mQTTEvent3.MQTTSubscribed)
				{
					_MQTTHelper.SubscribeAsync_Client(mQTTEvent3.TopicName);
				}
			}
			return;
		}
		if (string.IsNullOrEmpty(text2))
		{
			return;
		}
		MQTTObjectTopic mQTTEvent = getMQTTEvent(text2);
		if (mQTTEvent == null)
		{
			return;
		}
		foreach (MQTTObject mQTTObject in mQTTEvent.MQTTObjects)
		{
			MQActionEvent mQTTEvent2 = mQTTObject._MQTTEvent;
			if (!string.IsNullOrEmpty(text) && mQTTEvent2 != null && mQTTEvent2.EventName != null)
			{
				if (mQTTObject.op.DataType == typeof(CVServerResponse))
				{
					CVServerResponse data = JsonConvert.DeserializeObject<CVServerResponse>(JsonConvert.DeserializeObject<CVMQTTRequest>(text).Data.ToString());
					mQTTObject.op.TransferData(data);
				}
				else if (mQTTObject.op.DataType == typeof(CVMQTTRequest))
				{
					mQTTObject.op.TransferData(JsonConvert.DeserializeObject<CVMQTTRequest>(text));
				}
				else if (mQTTObject.op.DataType == typeof(CVBaseDataFlowResp))
				{
					mQTTObject.op.TransferData(JsonConvert.DeserializeObject<CVBaseDataFlowResp>(text));
				}
				else if (mQTTObject.op.DataType == typeof(bool))
				{
					mQTTObject.op.TransferData(true);
				}
			}
		}
	}
}
