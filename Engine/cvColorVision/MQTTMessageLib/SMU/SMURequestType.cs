using System.ComponentModel;

namespace MQTTMessageLib.SMU;

public enum SMURequestType
{
	[Description("打开")]
	Open,
	[Description("关闭")]
	Close,
	[Description("重新打开")]
	Reopen,
	[Description("测量")]
	Measure,
	[Description("扫描")]
	Scan,
	[Description("关闭输出")]
	CloseOutput,
	[Description("模板测量")]
	ModelMeasure,
	[Description("获取结果")]
	GetMeasureResult
}
