using System;
using System.Collections.Generic;
using CVCommCore;

namespace MQTTMessageLib.Algorithm;

public class POIResultCIEYExList : POIResultCIEList<POIResultDataCIEYEx>, IRuleActionEx, RuleAction
{
	public List<float> Lv_var { get; set; }

	public List<float> Lv_avg { get; set; }

	public bool GetResultData(ValidateRule rule, int idx)
	{
		float value = 0f;
		bool resultData = GetResultData(rule.RType, idx, out value);
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

	public bool GetResultData(ValidateRule rule)
	{
		throw new NotImplementedException();
	}

	private bool GetResultData(ValidateRuleType ruleType, int idx, out float value)
	{
		bool result = false;
		value = 0f;
		switch (ruleType)
		{
		case ValidateRuleType.CIE_lv_avg:
			value = Lv_avg[idx];
			result = true;
			break;
		case ValidateRuleType.CIE_lv_VAR:
			value = Lv_var[idx];
			result = true;
			break;
		}
		return result;
	}
}
