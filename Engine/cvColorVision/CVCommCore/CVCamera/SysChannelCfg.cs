using Newtonsoft.Json;

namespace CVCommCore.CVCamera;

public struct SysChannelCfg
{
	[JsonProperty("cfwport")]
	public int Cfwport { get; set; }

	[JsonProperty("chtype")]
	public ImageChannelType Chtype { get; set; }

	[JsonProperty("title")]
	public string ChannelTitle { get; set; }
}
