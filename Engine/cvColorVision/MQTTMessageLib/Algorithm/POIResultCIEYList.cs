using System;
using CVCommCore;

namespace MQTTMessageLib.Algorithm;

public class POIResultCIEYList : POIResultCIEList<POIResultDataCIEY>, RuleAction
{
	public float lv_var { get; set; }

	public float lv_dev { get; set; }

	public float lv_undis { get; set; }

	public float lv_avg { get; set; }

	public override void DoDataCalc()
	{
		double num = 0.0;
		float num2 = float.MinValue;
		float num3 = float.MaxValue;
		int num4 = 0;
		foreach (POIResultCIE<POIResultDataCIEY> datum in base.Data)
		{
			num += (double)datum.Data.Y;
			num2 = Math.Max(num2, datum.Data.Y);
			num3 = Math.Min(num3, datum.Data.Y);
			num4++;
		}
		lv_avg = (float)(num / (double)num4);
		num = 0.0;
		foreach (POIResultCIE<POIResultDataCIEY> datum2 in base.Data)
		{
			num += Math.Pow(datum2.Data.Y - lv_avg, 2.0);
		}
		num4 = base.Data.Count - 1;
		lv_var = (float)(num / (double)num4);
		lv_dev = (float)Math.Sqrt(lv_var);
		lv_undis = (num2 - num3) / (num2 + num3);
	}

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
		case ValidateRuleType.CIE_lv_avg:
			value = lv_avg;
			result = true;
			break;
		case ValidateRuleType.CIE_lv_VAR:
			value = lv_var;
			result = true;
			break;
		case ValidateRuleType.CIE_lv_STDEV:
			value = lv_dev;
			result = true;
			break;
		case ValidateRuleType.CIE_lv_UNDIS:
			value = lv_undis;
			result = true;
			break;
		}
		return result;
	}
}
