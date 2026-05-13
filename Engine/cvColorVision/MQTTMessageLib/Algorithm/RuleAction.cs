using System.Collections.Generic;
using CVCommCore;

namespace MQTTMessageLib.Algorithm;

public interface RuleAction
{
	List<ValidateRuleResult> RuleResult { get; }

	void DoRule();

	void DoDataCalc();

	bool GetResultData(ValidateRule rule);
}
