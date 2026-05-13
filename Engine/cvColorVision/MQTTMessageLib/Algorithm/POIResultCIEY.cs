using CVCommCore;
using CVCommCore.CVAlgorithm;

namespace MQTTMessageLib.Algorithm;

public class POIResultCIEY : POIResultCIE<POIResultDataCIEY>, RuleAction
{
	public POIResultCIEY(POIPoint point, POIResultDataCIEY data)
		: base(point, data)
	{
	}

	public void DoDataCalc()
	{
	}

	public bool GetResultData(ValidateRule rule)
	{
		float value = 0f;
		bool resultData = base.Data.GetResultData(rule.RType, out value);
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
}
