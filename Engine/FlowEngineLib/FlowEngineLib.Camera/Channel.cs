using System.Collections.Generic;
using Newtonsoft.Json;

namespace FlowEngineLib.Camera;

public class Channel
{
	private List<ChannelData> channels;

	public Channel()
	{
		channels = new List<ChannelData>();
	}

	public Channel(string json)
	{
		channels = JsonConvert.DeserializeObject<List<ChannelData>>(json);
	}

	public ChannelData Add(string typeCode, int FWPort, float Temp)
	{
		ChannelData channelData = new ChannelData(typeCode, FWPort, Temp);
		channels.Add(channelData);
		return channelData;
	}

	public static Channel From(string json)
	{
		return new Channel(json);
	}

	public string ToJsonString()
	{
		return JsonConvert.SerializeObject(channels);
	}

	public int GetChannelCount()
	{
		return channels.Count;
	}

	internal ChannelData GetChannel(int idx)
	{
		return channels[idx];
	}

	internal List<ChannelData> GetChannels()
	{
		return channels;
	}
}
