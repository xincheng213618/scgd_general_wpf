using log4net;
using ST.Library.UI;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.MQTT;

internal class MQTTBaseNode : STNode
{
	public static readonly ILog loginfo = LogManager.GetLogger(typeof(MQTTBaseNode));

	protected string _Server = "127.0.0.1";

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
			OnPropertyChanged();
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
			OnPropertyChanged();
		}
	}

	public MQTTBaseNode(string title)
	{
		base.Title = Lang.Get(title);
	}

}
