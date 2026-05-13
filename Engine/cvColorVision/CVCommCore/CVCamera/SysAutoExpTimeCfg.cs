using Newtonsoft.Json;

namespace CVCommCore.CVCamera;

public struct SysAutoExpTimeCfg
{
	[JsonProperty("autoExpFlag")]
	public bool AutoExpFlag { get; set; }

	[JsonProperty("autoExpTimeBegin")]
	public float AutoExpTimeBegin { get; set; }

	[JsonProperty("autoExpSyncFreq")]
	public float AutoExpSyncFreq { get; set; }

	[JsonProperty("autoExpSaturation")]
	public float AutoExpSaturation { get; set; }

	[JsonProperty("autoExpSatMaxAD")]
	public uint AutoExpSatMaxAD { get; set; }

	[JsonProperty("autoExpMaxPecentage")]
	public float AutoExpMaxPecentage { get; set; }

	[JsonProperty("autoExpSatDev")]
	public float AutoExpSatDev { get; set; }

	[JsonProperty("maxExpTime")]
	public float MaxExpTime { get; set; }

	[JsonProperty("minExpTime")]
	public float MinExpTime { get; set; }

	[JsonProperty("burstThreshold")]
	public float BurstThreshold { get; set; }
}
