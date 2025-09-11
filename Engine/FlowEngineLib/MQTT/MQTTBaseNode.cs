using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.MQTT;

internal class MQTTBaseNode : STNode
{
	public static readonly ILog loginfo = LogManager.GetLogger(typeof(MQTTBaseNode));

	protected string _Server;

	protected int _Port = 1883;

	protected MQTTHelper _MQTTHelper;

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

	public MQTTBaseNode(string title)
	{
		base.Title = title;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		string userName = "";
		string password = "";
		MQTTHelper.GetDefaultCfg(ref _Server, ref _Port, ref userName, ref password);
	}
}
