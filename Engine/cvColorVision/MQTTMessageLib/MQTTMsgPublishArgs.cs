namespace MQTTMessageLib;

public class MQTTMsgPublishArgs
{
	public string PublishTopic { get; }

	public string data { get; }

	public MQTTMsgPublishArgs(string publishTopic, string data)
	{
		PublishTopic = publishTopic;
		this.data = data;
	}
}
