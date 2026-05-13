using CVCommCore;

namespace MQTTMessageLib.Algorithm;

public class POIResultDataJND : IPOIResultData, IDataIndex
{
	public float v_jnd { get; set; }

	public float h_jnd { get; set; }

	public POIResultDataJND()
	{
	}

	public POIResultDataJND(float v_jnd, float h_jnd)
	{
		this.v_jnd = v_jnd;
		this.h_jnd = h_jnd;
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
		case MetricsResultDataType.JND_V:
			value = v_jnd;
			result = true;
			break;
		case MetricsResultDataType.JND_H:
			value = h_jnd;
			result = true;
			break;
		}
		return result;
	}

	public bool GetResultData(ValidateRuleType dataType, out float value)
	{
		bool result = false;
		value = 0f;
		switch (dataType)
		{
		case ValidateRuleType.JND_V:
			value = v_jnd;
			result = true;
			break;
		case ValidateRuleType.JND_H:
			value = h_jnd;
			result = true;
			break;
		}
		return result;
	}
}
