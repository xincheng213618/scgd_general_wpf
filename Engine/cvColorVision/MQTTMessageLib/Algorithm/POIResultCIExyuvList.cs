using System;
using System.Linq;
using CVCommCore;

namespace MQTTMessageLib.Algorithm;

public class POIResultCIExyuvList : POIResultCIEList<POIResultDataCIExyuv>, RuleAction
{
	public POIResultDataCIExyuv Mean { get; set; }

	public POIResultDataCIExyuv Variance { get; set; }

	public POIResultDataCIExyuv StandardDeviation { get; set; }

	public POIResultDataCIExyuv Uniformity { get; set; }

	public override void DoDataCalc()
	{
		double num = 0.0;
		double num2 = 0.0;
		double num3 = 0.0;
		double num4 = 0.0;
		double num5 = 0.0;
		double num6 = 0.0;
		double num7 = 0.0;
		double num8 = 0.0;
		double num9 = 0.0;
		int num10 = 0;
		POIResultDataCIExyuv pOIResultDataCIExyuv = new POIResultDataCIExyuv
		{
			x = base.Data.Max((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.x),
			y = base.Data.Max((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.y),
			u = base.Data.Max((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.u),
			v = base.Data.Max((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.v),
			X = base.Data.Max((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.X),
			Y = base.Data.Max((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.Y),
			Z = base.Data.Max((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.Z),
			CCT = base.Data.Max((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.CCT),
			Wave = base.Data.Max((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.Wave)
		};
		POIResultDataCIExyuv pOIResultDataCIExyuv2 = new POIResultDataCIExyuv
		{
			x = base.Data.Min((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.x),
			y = base.Data.Min((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.y),
			u = base.Data.Min((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.u),
			v = base.Data.Min((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.v),
			X = base.Data.Min((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.X),
			Y = base.Data.Min((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.Y),
			Z = base.Data.Min((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.Z),
			CCT = base.Data.Min((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.CCT),
			Wave = base.Data.Min((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.Wave)
		};
		Mean = new POIResultDataCIExyuv
		{
			CCT = base.Data.Average((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.CCT),
			Wave = base.Data.Average((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.Wave),
			X = base.Data.Average((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.X),
			Y = base.Data.Average((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.Y),
			Z = base.Data.Average((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.Z),
			x = base.Data.Average((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.x),
			y = base.Data.Average((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.y),
			u = base.Data.Average((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.u),
			v = base.Data.Average((POIResultCIE<POIResultDataCIExyuv> a) => a.Data.v)
		};
		num = 0.0;
		num2 = 0.0;
		num3 = 0.0;
		num4 = 0.0;
		num5 = 0.0;
		num6 = 0.0;
		num7 = 0.0;
		num8 = 0.0;
		num9 = 0.0;
		foreach (POIResultCIE<POIResultDataCIExyuv> datum in base.Data)
		{
			num += Math.Pow(datum.Data.x - Mean.x, 2.0);
			num2 += Math.Pow(datum.Data.y - Mean.y, 2.0);
			num3 += Math.Pow(datum.Data.u - Mean.u, 2.0);
			num4 += Math.Pow(datum.Data.v - Mean.v, 2.0);
			num5 += Math.Pow(datum.Data.Y - Mean.Y, 2.0);
			num6 += Math.Pow(datum.Data.X - Mean.X, 2.0);
			num7 += Math.Pow(datum.Data.Z - Mean.Z, 2.0);
			num8 += Math.Pow(datum.Data.CCT - Mean.CCT, 2.0);
			num9 += Math.Pow(datum.Data.Wave - Mean.Wave, 2.0);
		}
		num10 = base.Data.Count - 1;
		Variance = new POIResultDataCIExyuv
		{
			x = (float)(num / (double)num10),
			y = (float)(num2 / (double)num10),
			u = (float)(num3 / (double)num10),
			v = (float)(num4 / (double)num10),
			X = (float)(num6 / (double)num10),
			Y = (float)(num5 / (double)num10),
			Z = (float)(num7 / (double)num10),
			CCT = (float)(num8 / (double)num10),
			Wave = (float)(num9 / (double)num10)
		};
		StandardDeviation = new POIResultDataCIExyuv
		{
			x = (float)Math.Sqrt(Variance.x),
			y = (float)Math.Sqrt(Variance.y),
			u = (float)Math.Sqrt(Variance.u),
			v = (float)Math.Sqrt(Variance.v),
			X = (float)Math.Sqrt(Variance.X),
			Y = (float)Math.Sqrt(Variance.Y),
			Z = (float)Math.Sqrt(Variance.Z),
			CCT = (float)Math.Sqrt(Variance.CCT),
			Wave = (float)Math.Sqrt(Variance.Wave)
		};
		Uniformity = new POIResultDataCIExyuv
		{
			x = pOIResultDataCIExyuv2.x / pOIResultDataCIExyuv.x,
			y = pOIResultDataCIExyuv2.y / pOIResultDataCIExyuv.y,
			u = pOIResultDataCIExyuv2.u / pOIResultDataCIExyuv.u,
			v = pOIResultDataCIExyuv2.v / pOIResultDataCIExyuv.v,
			X = pOIResultDataCIExyuv2.X / pOIResultDataCIExyuv.X,
			Y = pOIResultDataCIExyuv2.Y / pOIResultDataCIExyuv.Y,
			Z = pOIResultDataCIExyuv2.Z / pOIResultDataCIExyuv.Z,
			CCT = pOIResultDataCIExyuv2.CCT / pOIResultDataCIExyuv.CCT,
			Wave = pOIResultDataCIExyuv2.Wave / pOIResultDataCIExyuv.Wave
		};
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
		bool flag = GetResultAvgData(ruleType, out value);
		if (!flag)
		{
			flag = GetResultVARData(ruleType, out value);
		}
		if (!flag)
		{
			flag = GetResultSTDEVData(ruleType, out value);
		}
		if (!flag)
		{
			flag = GetResultUNDISData(ruleType, out value);
		}
		return flag;
	}

	private bool GetResultAvgData(ValidateRuleType ruleType, out float value)
	{
		bool result = false;
		value = 0f;
		switch (ruleType)
		{
		case ValidateRuleType.CIE_x_avg:
			value = Mean.x;
			result = true;
			break;
		case ValidateRuleType.CIE_y_avg:
			value = Mean.y;
			result = true;
			break;
		case ValidateRuleType.CIE_u_avg:
			value = Mean.u;
			result = true;
			break;
		case ValidateRuleType.CIE_v_avg:
			value = Mean.v;
			result = true;
			break;
		case ValidateRuleType.CIE_lv_avg:
		case ValidateRuleType.CIE_Y_avg:
			value = Mean.Y;
			result = true;
			break;
		case ValidateRuleType.CIE_X_avg:
			value = Mean.X;
			result = true;
			break;
		case ValidateRuleType.CIE_Z_avg:
			value = Mean.Z;
			result = true;
			break;
		case ValidateRuleType.CCT_avg:
			value = Mean.CCT;
			result = true;
			break;
		case ValidateRuleType.Wave_avg:
			value = Mean.Wave;
			result = true;
			break;
		}
		return result;
	}

	private bool GetResultVARData(ValidateRuleType ruleType, out float value)
	{
		bool result = false;
		value = 0f;
		switch (ruleType)
		{
		case ValidateRuleType.CIE_x_VAR:
			value = Variance.x;
			result = true;
			break;
		case ValidateRuleType.CIE_y_VAR:
			value = Variance.y;
			result = true;
			break;
		case ValidateRuleType.CIE_u_VAR:
			value = Variance.u;
			result = true;
			break;
		case ValidateRuleType.CIE_v_VAR:
			value = Variance.v;
			result = true;
			break;
		case ValidateRuleType.CIE_lv_VAR:
		case ValidateRuleType.CIE_Y_VAR:
			value = Variance.Y;
			result = true;
			break;
		case ValidateRuleType.CIE_X_VAR:
			value = Variance.X;
			result = true;
			break;
		case ValidateRuleType.CIE_Z_VAR:
			value = Variance.Z;
			result = true;
			break;
		case ValidateRuleType.CCT_VAR:
			value = Variance.CCT;
			result = true;
			break;
		case ValidateRuleType.Wave_VAR:
			value = Variance.Wave;
			result = true;
			break;
		}
		return result;
	}

	private bool GetResultSTDEVData(ValidateRuleType ruleType, out float value)
	{
		bool result = false;
		value = 0f;
		switch (ruleType)
		{
		case ValidateRuleType.CIE_x_STDEV:
			value = StandardDeviation.x;
			result = true;
			break;
		case ValidateRuleType.CIE_y_STDEV:
			value = StandardDeviation.y;
			result = true;
			break;
		case ValidateRuleType.CIE_u_STDEV:
			value = StandardDeviation.u;
			result = true;
			break;
		case ValidateRuleType.CIE_v_STDEV:
			value = StandardDeviation.v;
			result = true;
			break;
		case ValidateRuleType.CIE_lv_STDEV:
		case ValidateRuleType.CIE_Y_STDEV:
			value = StandardDeviation.Y;
			result = true;
			break;
		case ValidateRuleType.CIE_X_STDEV:
			value = StandardDeviation.X;
			result = true;
			break;
		case ValidateRuleType.CIE_Z_STDEV:
			value = StandardDeviation.Z;
			result = true;
			break;
		case ValidateRuleType.CCT_STDEV:
			value = StandardDeviation.CCT;
			result = true;
			break;
		case ValidateRuleType.Wave_STDEV:
			value = StandardDeviation.Wave;
			result = true;
			break;
		}
		return result;
	}

	private bool GetResultUNDISData(ValidateRuleType ruleType, out float value)
	{
		bool result = false;
		value = 0f;
		switch (ruleType)
		{
		case ValidateRuleType.CIE_x_UNDIS:
			value = Uniformity.x;
			result = true;
			break;
		case ValidateRuleType.CIE_y_UNDIS:
			value = Uniformity.y;
			result = true;
			break;
		case ValidateRuleType.CIE_u_UNDIS:
			value = Uniformity.u;
			result = true;
			break;
		case ValidateRuleType.CIE_v_UNDIS:
			value = Uniformity.v;
			result = true;
			break;
		case ValidateRuleType.CIE_lv_UNDIS:
		case ValidateRuleType.CIE_Y_UNDIS:
			value = Uniformity.Y;
			result = true;
			break;
		case ValidateRuleType.CIE_X_UNDIS:
			value = Uniformity.X;
			result = true;
			break;
		case ValidateRuleType.CIE_Z_UNDIS:
			value = Uniformity.Z;
			result = true;
			break;
		case ValidateRuleType.CCT_UNDIS:
			value = Uniformity.CCT;
			result = true;
			break;
		case ValidateRuleType.Wave_UNDIS:
			value = Uniformity.Wave;
			result = true;
			break;
		}
		return result;
	}
}
