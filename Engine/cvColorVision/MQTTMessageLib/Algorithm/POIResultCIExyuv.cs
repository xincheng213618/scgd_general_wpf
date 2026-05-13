using CVCommCore;
using CVCommCore.CVAlgorithm;

namespace MQTTMessageLib.Algorithm;

public class POIResultCIExyuv : POIResultCIE<POIResultDataCIExyuv>, RuleAction
{
	public POIResultCIExyuv(POIPoint point, POIResultDataCIExyuv data)
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
