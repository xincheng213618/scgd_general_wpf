using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.OLED;

public class OLEDImageCroppingParam : AlgorithmImageParam
{
	public int ROI_MasterId { get; set; }

	public OLEDImageCroppingParam()
	{
		base.MasterId = -1;
		ROI_MasterId = -1;
	}
}
