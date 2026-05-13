using System.Collections.Generic;

namespace CVCommCore.CVAlgorithm;

public class POIPointInfo
{
	public POIHeaderInfo HeaderInfo { get; set; }

	public List<POIPointPosition> Positions { get; set; }
}
