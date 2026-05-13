using System.ComponentModel;

namespace MQTTMessageLib.Calibration;

public enum CalibrationRequestType
{
	[Description("校正获取数据")]
	Calibration_GetData,
	[Description("删除数据")]
	DeleteData,
	[Description("Tif合并")]
	Tif_Merge,
	[Description("校正SetROI")]
	SetROI,
	[Description("ND复位")]
	NDReset
}
