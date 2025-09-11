using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.Algorithm;

public class AlgorithmOLEDAOIParam : AlgorithmParam
{
	public bool VhLineEnable { get; set; }

	public bool MuraEnable { get; set; }

	public bool PixelDefectEnable { get; set; }

	public string ResultDataFileName { get; set; }

	public AlgorithmOLEDAOIParam(CVOLED_COLOR color, string outputFileName, bool vhLineEnable, bool muraEnable, bool pixelDefectEnable)
	{
		base.Color = color;
		ResultDataFileName = outputFileName;
		VhLineEnable = vhLineEnable;
		MuraEnable = muraEnable;
		PixelDefectEnable = pixelDefectEnable;
	}
}
