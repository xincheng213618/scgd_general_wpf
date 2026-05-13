using Newtonsoft.Json;

namespace MQTTMessageLib.Calibration;

public class CalibrationItemCfg
{
	public bool Selected { get; set; }

	public string Title { get; set; }

	public string FileName { get; set; }

	public int ResId { get; set; }

	public CalibrationType CalibrationType { get; set; }

	[JsonIgnore]
	public bool IsValid => !string.IsNullOrWhiteSpace(Title);

	public bool IsColor()
	{
		if (CalibrationType != CalibrationType.LumOneColor && CalibrationType != CalibrationType.LumFourColor)
		{
			return CalibrationType == CalibrationType.LumMultiColor;
		}
		return true;
	}

	public bool IsLuminance()
	{
		return CalibrationType == CalibrationType.Luminance;
	}
}
