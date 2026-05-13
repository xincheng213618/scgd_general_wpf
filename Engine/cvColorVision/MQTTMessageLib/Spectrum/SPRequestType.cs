using System.ComponentModel;

namespace MQTTMessageLib.Spectrum;

public enum SPRequestType
{
	[Description("打开")]
	Open,
	[Description("关闭")]
	Close,
	[Description("重新打开")]
	Reopen,
	[Description("测量")]
	Measure,
	[Description("测量EQE")]
	MeasureEQE,
	[Description("自动测量")]
	MeasureAuto,
	[Description("EQE自动测量")]
	EQEMeasureAuto,
	[Description("自动测量停止")]
	MeasureAutoStop,
	[Description("校零")]
	ZeroCalibration,
	[Description("自适应校零")]
	SelfAdaptionInitDark,
	[Description("设置参数")]
	SetSysParam,
	[Description("获取参数")]
	GetSysParam,
	[Description("Shutter打开")]
	ShutterOpen,
	[Description("Shutter关闭")]
	ShutterClose,
	[Description("Shutter连接")]
	ShutterConnect,
	[Description("Shutter断开")]
	ShutterDisconnect,
	[Description("获取All ID")]
	Scan,
	[Description("设置NDPort")]
	SetNDPort,
	[Description("获取NDPort")]
	GetNDPort
}
