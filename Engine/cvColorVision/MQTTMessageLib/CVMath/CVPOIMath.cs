using System.Collections.Generic;
using CVCommCore;
using MQTTMessageLib.Algorithm;

namespace MQTTMessageLib.CVMath;

public abstract class CVPOIMath<T> where T : IDataIndex
{
	public abstract Dictionary<DataMetricsModel, T> DoCalc(List<POIResultCIE<T>> Data);
}
