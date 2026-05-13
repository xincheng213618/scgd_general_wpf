namespace MQTTMessageLib.Calibration;

public struct CalibrationParamItem
{
	public CalibrationType CaliType { get; set; }

	public string IsSelectedItemName { get; set; }

	public string ItemName { get; set; }

	public string ItemId { get; set; }
}
