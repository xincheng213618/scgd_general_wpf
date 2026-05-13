using System.ComponentModel;

namespace MQTTMessageLib.Calibration;

public enum CalibrationResultType
{
	[Description("校正获取数据")]
	Calibration,
	[Description("删除数据")]
	DeleteData,
	[Description("Tif合并")]
	Tif_Merge,
	[Description("SetROI")]
	SetROI,
	[Description("ND复位")]
	NDReset
}
