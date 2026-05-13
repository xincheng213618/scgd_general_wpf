using CVCommCore;
using CVCommCore.CVAlgorithm;

namespace MQTTMessageLib.Algorithm;

public class POIResultCIEXYZOnly : POIResultCIEOnly<POIResultDataCIExyuv>
{
	public DataMetricsModel MetricsType { get; set; }

	public POIResultCIEXYZOnly(POIPointOnly point, POIResultDataCIExyuv data)
		: base(point, data)
	{
		if (point != null)
		{
			MetricsType = DataMetricsModel.SinglePoint;
		}
		else
		{
			MetricsType = DataMetricsModel.Mean;
		}
	}
}
