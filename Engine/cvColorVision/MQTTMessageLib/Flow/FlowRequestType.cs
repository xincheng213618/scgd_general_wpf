using System.ComponentModel;

namespace MQTTMessageLib.Flow;

public enum FlowRequestType
{
	[Description("加载")]
	Load,
	[Description("运行")]
	Run,
	[Description("停止")]
	Stop,
	[Description("复合流程停止")]
	StopCombined,
	[Description("复合流程运行")]
	CombinedRun,
	[Description("获取复合流程结果")]
	GetCombinedResult
}
