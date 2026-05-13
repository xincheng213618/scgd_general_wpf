using CVCommCore;

namespace MQTTMessageLib.Algorithm;

public interface IPOIResultDataEx : IDataIndex
{
	bool GetResultData(MetricsResultDataType dataType, int idx, out float value);

	bool GetResultData(ValidateRuleType dataType, int idx, out float value);
}
