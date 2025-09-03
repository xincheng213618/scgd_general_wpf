using FlowEngineLib.MQTT;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

[STNode("/10 MQTT")]
internal class MQTTCustomPublishNode : MQTTBaseNode
{
	private string _Data;

	private string _Topic;

	private STNodeOption m_in_start;

	[STNodeProperty("Data", "Data")]
	public string Data
	{
		get
		{
			return _Data;
		}
		set
		{
			_Data = value;
		}
	}

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

	public MQTTCustomPublishNode()
		: base("MQTT自定义发布")
	{
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_in_start = new STNodeOption("", typeof(bool), bSingle: false);
		base.InputOptions.Add(m_in_start);
		m_in_start.Connected += m_in_start_Connected;
		m_in_start.DataTransfer += m_in_start_DataTransfer;
	}

	private void m_in_start_DataTransfer(object sender, STNodeOptionEventArgs e)
	{
		if (e.Status == ConnectionStatus.Connected && e.TargetOption.Data != null && (bool)e.TargetOption.Data)
		{
			if (string.IsNullOrEmpty(_Data))
			{
				_Data = " ";
			}
			_MQTTHelper.PublishAsync_Client(_Topic, _Data, retained: false);
		}
	}

	private void m_in_start_Connected(object sender, STNodeOptionEventArgs e)
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
