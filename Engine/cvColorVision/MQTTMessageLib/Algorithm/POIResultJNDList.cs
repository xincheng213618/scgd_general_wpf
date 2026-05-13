using CVCommCore;

namespace MQTTMessageLib.Algorithm;

public class POIResultJNDList : POIResultCIEList<POIResultDataJND>, RuleAction
{
	public float v_jnd { get; set; }

	public float h_jnd { get; set; }

	public bool GetResultData(ValidateRule rule)
	{
		float value = 0f;
		bool resultData = GetResultData(rule.RType, out value);
		if (resultData)
		{
			base.RuleResult.Add(new ValidateRuleResult
			{
				Rule = rule,
				Value = value
			});
		}
		return resultData;
	}

	private bool GetResultData(ValidateRuleType ruleType, out float value)
	{
		bool result = false;
		value = 0f;
		switch (ruleType)
		{
		case ValidateRuleType.JND_V:
			value = v_jnd;
			result = true;
			break;
		case ValidateRuleType.JND_H:
			value = h_jnd;
			result = true;
			break;
		}
		return result;
	}
}
