using System.Collections.Generic;
using CVCommCore;

namespace MQTTMessageLib.Algorithm;

public class POIResultDataCIEYEx : IPOIResultDataEx, IDataIndex
{
	public List<float> Y { get; set; }

	public int GetDataCount()
	{
		return Y.Count;
	}

	public POIResultDataCIEYEx(List<float> y)
	{
		Y = y;
	}

	public POIResultDataCIEYEx(List<POIResultDataCIEY> y)
	{
		Y = new List<float>();
		foreach (POIResultDataCIEY item in y)
		{
			Y.Add(item.Y);
		}
	}

	public bool GetResultData(MetricsResultDataType dataType, int idx, out float value)
	{
		bool result = false;
		value = 0f;
		int index = 0;
		if (idx >= 0 && idx < Y.Count - 1)
		{
			index = idx;
		}
		if (dataType == MetricsResultDataType.CIE_lv || dataType == MetricsResultDataType.CIE_Y)
		{
			value = Y[index];
			result = true;
		}
		return result;
	}

	public bool GetResultData(ValidateRuleType ruleType, int idx, out float value)
	{
		bool result = false;
		value = 0f;
		int index = 0;
		if (idx >= 0 && idx < Y.Count - 1)
		{
			index = idx;
		}
		if (ruleType == ValidateRuleType.CIE_lv || ruleType == ValidateRuleType.CIE_Y)
		{
			value = Y[index];
			result = true;
		}
		return result;
	}
}
