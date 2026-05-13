using System.Collections.Generic;
using CVCommCore.CVAlgorithm;

namespace MQTTMessageLib.Algorithm;

public struct FindSFRROIResult
{
	public POIPointOnly POI { get; set; }

	public List<ROISFRResult> ROIResults { get; set; }
}
