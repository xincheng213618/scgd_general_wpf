using CVCommCore;

namespace MQTTMessageLib.Algorithm;

public class POIResultDataCIExyuv : IPOIResultData, IDataIndex
{
	public float CCT { get; set; }

	public float Wave { get; set; }

	public float X { get; set; }

	public float Y { get; set; }

	public float Z { get; set; }

	public float x { get; set; }

	public float y { get; set; }

	public float u { get; set; }

	public float v { get; set; }

	public POIResultDataCIExyuv()
	{
	}

	public POIResultDataCIExyuv(float CCT, float Wave, float X, float Y, float Z, float x, float y, float u, float v)
		: this()
	{
		this.CCT = CCT;
		this.Wave = Wave;
		this.X = X;
		this.Y = Y;
		this.Z = Z;
		this.x = x;
		this.y = y;
		this.u = u;
		this.v = v;
	}

	public int GetDataCount()
	{
		return 1;
	}

	public bool GetResultData(MetricsResultDataType dataType, out float value)
	{
		bool result = false;
		value = 0f;
		switch (dataType)
		{
		case MetricsResultDataType.CIE_x:
			value = x;
			result = true;
			break;
		case MetricsResultDataType.CIE_y:
			value = y;
			result = true;
			break;
		case MetricsResultDataType.CIE_u:
			value = u;
			result = true;
			break;
		case MetricsResultDataType.CIE_v:
			value = v;
			result = true;
			break;
		case MetricsResultDataType.CIE_lv:
		case MetricsResultDataType.CIE_Y:
			value = Y;
			result = true;
			break;
		case MetricsResultDataType.CIE_X:
			value = X;
			result = true;
			break;
		case MetricsResultDataType.CIE_Z:
			value = Z;
			result = true;
			break;
		case MetricsResultDataType.CCT:
			value = CCT;
			result = true;
			break;
		case MetricsResultDataType.Wave:
			value = Wave;
			result = true;
			break;
		}
		return result;
	}

	public bool GetResultData(out float valueX, out float valueY, out float valueZ)
	{
		valueX = X;
		valueY = Y;
		valueZ = Z;
		return true;
	}

	public bool GetResultData(ValidateRuleType ruleType, out float value)
	{
		bool result = false;
		value = 0f;
		switch (ruleType)
		{
		case ValidateRuleType.CIE_x:
			value = x;
			result = true;
			break;
		case ValidateRuleType.CIE_y:
			value = y;
			result = true;
			break;
		case ValidateRuleType.CIE_u:
			value = u;
			result = true;
			break;
		case ValidateRuleType.CIE_v:
			value = v;
			result = true;
			break;
		case ValidateRuleType.CIE_lv:
		case ValidateRuleType.CIE_Y:
			value = Y;
			result = true;
			break;
		case ValidateRuleType.CIE_X:
			value = X;
			result = true;
			break;
		case ValidateRuleType.CIE_Z:
			value = Z;
			result = true;
			break;
		case ValidateRuleType.CCT:
			value = CCT;
			result = true;
			break;
		case ValidateRuleType.Wave:
			value = Wave;
			result = true;
			break;
		}
		return result;
	}
}
