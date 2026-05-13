using CVCommCore;

namespace MQTTMessageLib.Algorithm;

public interface IRuleActionEx : RuleAction
{
	bool GetResultData(ValidateRule rule, int idx);
}
