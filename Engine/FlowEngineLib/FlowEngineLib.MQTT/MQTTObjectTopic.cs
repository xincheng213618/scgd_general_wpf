using System.Collections.Generic;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.MQTT;

internal class MQTTObjectTopic
{
	public bool MQTTSubscribed;

	public string TopicName;

	public List<MQTTObject> MQTTObjects;

	private static readonly object _lock = new object();

	public MQTTObjectTopic(string topicName)
	{
		MQTTObjects = new List<MQTTObject>();
		MQTTSubscribed = false;
		TopicName = topicName;
	}

	public bool removeMQTTEvent(STNodeOption op)
	{
		lock (_lock)
		{
			foreach (MQTTObject mQTTObject in MQTTObjects)
			{
				if (mQTTObject.op.Equals(op))
				{
					MQTTObjects.Remove(mQTTObject);
					return true;
				}
			}
			return false;
		}
	}

	public bool hasMQTTEvent(STNodeOption op)
	{
		lock (_lock)
		{
			foreach (MQTTObject mQTTObject in MQTTObjects)
			{
				if (mQTTObject.op.Equals(op))
				{
					return true;
				}
			}
			return false;
		}
	}

	internal void AddMQTTEvent(MQTTObject mQTT)
	{
		lock (_lock)
		{
			MQTTObjects.Add(mQTT);
		}
	}
}
