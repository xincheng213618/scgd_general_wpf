using System.Collections.Generic;
using CVCommCore;
using CVCommCore.CVAlgorithm;

namespace MQTTMessageLib.Algorithm;

public class POIResultCIE<T> : POIResultCIEData<T> where T : IDataIndex
{
	public List<ValidateRuleResult> RuleResult { get; set; }

	public POIResultCIE(POIPoint point, T data)
		: base(point, data)
	{
		RuleResult = new List<ValidateRuleResult>();
	}

	public void DoRule()
	{
		foreach (ValidateRuleResult item in RuleResult)
		{
			item.Calculated();
		}
	}
}
