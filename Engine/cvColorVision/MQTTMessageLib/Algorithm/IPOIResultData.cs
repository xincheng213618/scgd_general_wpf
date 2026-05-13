using CVCommCore;

namespace MQTTMessageLib.Algorithm;

public interface IPOIResultData : IDataIndex
{
	bool GetResultData(MetricsResultDataType dataType, out float value);

	bool GetResultData(ValidateRuleType dataType, out float value);
}
