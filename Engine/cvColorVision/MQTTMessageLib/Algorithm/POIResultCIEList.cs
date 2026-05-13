using System.Collections.Generic;
using CVCommCore;

namespace MQTTMessageLib.Algorithm;

public class POIResultCIEList<T> where T : IDataIndex
{
	public List<POIResultCIE<T>> Data { get; set; }

	public List<ValidateRuleResult> RuleResult { get; set; }

	public POIResultCIEList()
	{
		Data = new List<POIResultCIE<T>>();
		RuleResult = new List<ValidateRuleResult>();
	}

	public virtual void DoDataCalc()
	{
	}

	public virtual void DoRule()
	{
		foreach (ValidateRuleResult item in RuleResult)
		{
			item.Calculated();
		}
	}
}
