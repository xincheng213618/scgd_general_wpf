namespace CVCommCore;

public class ValidateRuleResult
{
	private ValidateRule _Rule;

	private CalculatedType _CalcType;

	public ValidateRule Rule
	{
		get
		{
			return _Rule;
		}
		set
		{
			_Rule = value;
			SetCalcType();
		}
	}

	public float Value { get; set; }

	public ValidateRuleResultType Result { get; set; }

	public static MetricsResultDataType GetCIEDataType(ValidateRuleType ruleType)
	{
		MetricsResultDataType result = MetricsResultDataType.None;
		switch (ruleType)
		{
		case ValidateRuleType.CIE_x:
		case ValidateRuleType.CIE_x_avg:
		case ValidateRuleType.CIE_x_VAR:
		case ValidateRuleType.CIE_x_STDEV:
		case ValidateRuleType.CIE_x_UNDIS:
			result = MetricsResultDataType.CIE_x;
			break;
		case ValidateRuleType.CIE_y:
		case ValidateRuleType.CIE_y_avg:
		case ValidateRuleType.CIE_y_VAR:
		case ValidateRuleType.CIE_y_STDEV:
		case ValidateRuleType.CIE_y_UNDIS:
			result = MetricsResultDataType.CIE_y;
			break;
		case ValidateRuleType.CIE_u:
		case ValidateRuleType.CIE_u_avg:
		case ValidateRuleType.CIE_u_VAR:
		case ValidateRuleType.CIE_u_STDEV:
		case ValidateRuleType.CIE_u_UNDIS:
			result = MetricsResultDataType.CIE_u;
			break;
		case ValidateRuleType.CIE_v:
		case ValidateRuleType.CIE_v_avg:
		case ValidateRuleType.CIE_v_VAR:
		case ValidateRuleType.CIE_v_STDEV:
		case ValidateRuleType.CIE_v_UNDIS:
			result = MetricsResultDataType.CIE_v;
			break;
		case ValidateRuleType.CIE_lv:
		case ValidateRuleType.CIE_Y:
		case ValidateRuleType.CIE_lv_avg:
		case ValidateRuleType.CIE_Y_avg:
		case ValidateRuleType.CIE_lv_VAR:
		case ValidateRuleType.CIE_Y_VAR:
		case ValidateRuleType.CIE_lv_STDEV:
		case ValidateRuleType.CIE_Y_STDEV:
		case ValidateRuleType.CIE_lv_UNDIS:
		case ValidateRuleType.CIE_Y_UNDIS:
			result = MetricsResultDataType.CIE_Y;
			break;
		case ValidateRuleType.CIE_X_avg:
		case ValidateRuleType.CIE_X_VAR:
		case ValidateRuleType.CIE_X_STDEV:
		case ValidateRuleType.CIE_X_UNDIS:
			result = MetricsResultDataType.CIE_X;
			break;
		case ValidateRuleType.CIE_Z:
		case ValidateRuleType.CIE_Z_avg:
		case ValidateRuleType.CIE_Z_VAR:
		case ValidateRuleType.CIE_Z_STDEV:
		case ValidateRuleType.CIE_Z_UNDIS:
			result = MetricsResultDataType.CIE_Z;
			break;
		case ValidateRuleType.CCT:
		case ValidateRuleType.CCT_avg:
		case ValidateRuleType.CCT_VAR:
		case ValidateRuleType.CCT_STDEV:
		case ValidateRuleType.CCT_UNDIS:
			result = MetricsResultDataType.CCT;
			break;
		case ValidateRuleType.Wave:
		case ValidateRuleType.Wave_avg:
		case ValidateRuleType.Wave_VAR:
		case ValidateRuleType.Wave_STDEV:
		case ValidateRuleType.Wave_UNDIS:
			result = MetricsResultDataType.Wave;
			break;
		case ValidateRuleType.JND_V:
			result = MetricsResultDataType.JND_V;
			break;
		case ValidateRuleType.JND_H:
			result = MetricsResultDataType.JND_H;
			break;
		}
		return result;
	}

	public static DataMetricsModel GetDataMetricsType(ValidateRuleType ruleType)
	{
		DataMetricsModel result = DataMetricsModel.None;
		switch (ruleType)
		{
		case ValidateRuleType.CIE_x:
		case ValidateRuleType.CIE_y:
		case ValidateRuleType.CIE_u:
		case ValidateRuleType.CIE_v:
		case ValidateRuleType.CIE_lv:
		case ValidateRuleType.CIE_X:
		case ValidateRuleType.CIE_Y:
		case ValidateRuleType.CIE_Z:
		case ValidateRuleType.CCT:
		case ValidateRuleType.Wave:
			result = DataMetricsModel.SinglePoint;
			break;
		case ValidateRuleType.CIE_x_avg:
		case ValidateRuleType.CIE_y_avg:
		case ValidateRuleType.CIE_u_avg:
		case ValidateRuleType.CIE_v_avg:
		case ValidateRuleType.CIE_lv_avg:
		case ValidateRuleType.CIE_X_avg:
		case ValidateRuleType.CIE_Y_avg:
		case ValidateRuleType.CIE_Z_avg:
		case ValidateRuleType.CCT_avg:
		case ValidateRuleType.Wave_avg:
			result = DataMetricsModel.Mean;
			break;
		case ValidateRuleType.CIE_x_VAR:
		case ValidateRuleType.CIE_y_VAR:
		case ValidateRuleType.CIE_u_VAR:
		case ValidateRuleType.CIE_v_VAR:
		case ValidateRuleType.CIE_lv_VAR:
		case ValidateRuleType.CIE_X_VAR:
		case ValidateRuleType.CIE_Y_VAR:
		case ValidateRuleType.CIE_Z_VAR:
		case ValidateRuleType.CCT_VAR:
		case ValidateRuleType.Wave_VAR:
			result = DataMetricsModel.Variance;
			break;
		case ValidateRuleType.CIE_x_STDEV:
		case ValidateRuleType.CIE_y_STDEV:
		case ValidateRuleType.CIE_u_STDEV:
		case ValidateRuleType.CIE_v_STDEV:
		case ValidateRuleType.CIE_lv_STDEV:
		case ValidateRuleType.CIE_X_STDEV:
		case ValidateRuleType.CIE_Y_STDEV:
		case ValidateRuleType.CIE_Z_STDEV:
		case ValidateRuleType.CCT_STDEV:
		case ValidateRuleType.Wave_STDEV:
			result = DataMetricsModel.StandardDeviation;
			break;
		case ValidateRuleType.CIE_x_UNDIS:
		case ValidateRuleType.CIE_y_UNDIS:
		case ValidateRuleType.CIE_u_UNDIS:
		case ValidateRuleType.CIE_v_UNDIS:
		case ValidateRuleType.CIE_lv_UNDIS:
		case ValidateRuleType.CIE_X_UNDIS:
		case ValidateRuleType.CIE_Y_UNDIS:
		case ValidateRuleType.CIE_Z_UNDIS:
		case ValidateRuleType.CCT_UNDIS:
		case ValidateRuleType.Wave_UNDIS:
			result = DataMetricsModel.Uniformity;
			break;
		case ValidateRuleType.JND_V:
		case ValidateRuleType.JND_H:
			result = DataMetricsModel.SinglePoint;
			break;
		default:
			result = DataMetricsModel.None;
			break;
		case ValidateRuleType.None:
			break;
		}
		return result;
	}

	private void SetCalcType()
	{
		if (_Rule.Max.HasValue && _Rule.Min.HasValue)
		{
			_CalcType = CalculatedType.MaxMin;
		}
		else if (_Rule.Max.HasValue)
		{
			_CalcType = CalculatedType.Max;
		}
		else if (_Rule.Min.HasValue)
		{
			_CalcType = CalculatedType.Min;
		}
		else if (!string.IsNullOrEmpty(_Rule.Equal))
		{
			_CalcType = CalculatedType.Equal;
		}
	}

	public void Calculated()
	{
		Result = ValidateRuleResultType.None;
		switch (_CalcType)
		{
		case CalculatedType.MaxMin:
			Calculated_MaxMin();
			break;
		case CalculatedType.Max:
			Calculated_Max();
			break;
		case CalculatedType.Min:
			Calculated_Min();
			break;
		case CalculatedType.Equal:
			Calculated_Equal();
			break;
		}
	}

	private void Calculated_Equal()
	{
		if (Value.ToString("F" + Rule.Radix).Equals(Rule.Equal))
		{
			Result = ValidateRuleResultType.T;
		}
		else
		{
			Result = ValidateRuleResultType.F;
		}
	}

	private void Calculated_Min()
	{
		if (Value > Rule.Min)
		{
			Result = ValidateRuleResultType.T;
		}
		else
		{
			Result = ValidateRuleResultType.F;
		}
	}

	private void Calculated_Max()
	{
		if (Value < Rule.Max)
		{
			Result = ValidateRuleResultType.T;
		}
		else
		{
			Result = ValidateRuleResultType.F;
		}
	}

	public void Calculated_MaxMin()
	{
		if (Value >= Rule.Min && Value <= Rule.Max)
		{
			Result = ValidateRuleResultType.M;
		}
		else if (Value > Rule.Max)
		{
			Result = ValidateRuleResultType.H;
		}
		else if (Value < Rule.Min)
		{
			Result = ValidateRuleResultType.L;
		}
	}
}
