using CVCommCore;

namespace MQTTMessageLib.Algorithm;

public class POIResultDataCIEY : IPOIResultData, IDataIndex
{
	public float Y { get; set; }

	public POIResultDataCIEY(float y)
	{
		Y = y;
	}

	public int GetDataCount()
	{
		return 1;
	}

	public bool GetResultData(MetricsResultDataType dataType, out float value)
	{
		bool result = false;
		value = 0f;
		if (dataType == MetricsResultDataType.CIE_lv || dataType == MetricsResultDataType.CIE_Y)
		{
			value = Y;
			result = true;
		}
		return result;
	}

	public bool GetResultData(ValidateRuleType ruleType, out float value)
	{
		bool result = false;
		value = 0f;
		if (ruleType == ValidateRuleType.CIE_lv || ruleType == ValidateRuleType.CIE_Y)
		{
			value = Y;
			result = true;
		}
		return result;
	}
}
