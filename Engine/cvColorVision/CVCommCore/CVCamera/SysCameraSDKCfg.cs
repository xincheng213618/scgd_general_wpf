using Newtonsoft.Json;

namespace CVCommCore.CVCamera;

public struct SysCameraSDKCfg
{
	[JsonProperty("ob")]
	public int Ob { get; set; }

	[JsonProperty("obR")]
	public int ObR { get; set; }

	[JsonProperty("obT")]
	public int ObT { get; set; }

	[JsonProperty("obB")]
	public int ObB { get; set; }

	[JsonProperty("tempCtlChecked")]
	public bool TempCtlChecked { get; set; }

	[JsonProperty("targetTemp")]
	public float TargetTemp { get; set; }

	[JsonProperty("usbTraffic")]
	public float UsbTraffic { get; set; }

	[JsonProperty("offset")]
	public int Offset { get; set; }

	[JsonProperty("gain")]
	public int Gain { get; set; }

	[JsonProperty("ex")]
	public int ROI_x { get; set; }

	[JsonProperty("ey")]
	public int ROI_y { get; set; }

	[JsonProperty("ew")]
	public int ROI_Width { get; set; }

	[JsonProperty("eh")]
	public int ROI_Height { get; set; }

	public int TempSpanTime { get; set; }
}
