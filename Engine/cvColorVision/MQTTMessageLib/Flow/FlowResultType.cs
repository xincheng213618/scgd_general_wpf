using System.ComponentModel;

namespace MQTTMessageLib.Flow;

public enum FlowResultType
{
	[Description("加载")]
	Load,
	[Description("运行")]
	Run,
	[Description("停止")]
	Stop,
	[Description("组合运行")]
	CombinedRun,
	[Description("获取复合流程结果")]
	GetCombinedResult
}
