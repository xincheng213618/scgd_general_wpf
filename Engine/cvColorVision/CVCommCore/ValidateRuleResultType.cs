using System.ComponentModel;

namespace CVCommCore;

public enum ValidateRuleResultType
{
	None = -1,
	[Description("之间")]
	M,
	[Description("高于")]
	H,
	[Description("低于")]
	L,
	[Description("True")]
	T,
	[Description("False")]
	F
}
