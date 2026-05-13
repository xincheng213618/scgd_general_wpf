using System.ComponentModel;

namespace MQTTMessageLib.PG;

public enum PGRequestType
{
	[Description("打开")]
	Open,
	[Description("关闭")]
	Close,
	[Description("重新打开")]
	Reopen,
	[Description("执行命令")]
	ExecCmds,
	[Description("开始")]
	Start,
	[Description("停止")]
	Stop,
	[Description("重置")]
	Reset,
	[Description("上")]
	SwitchUp,
	[Description("下")]
	SwitchDown,
	[Description("指定")]
	SwitchFrame,
	[Description("自定义")]
	Custom
}
