using CVCommCore.CVAlgorithm;

namespace MQTTMessageLib.Algorithm.POI;

public class BuildCADMappingParam
{
	public int CAD_MasterId { get; set; }

	public string CAD_PosFileName { get; set; }

	public PointFloat[] ROI { get; set; }
}
