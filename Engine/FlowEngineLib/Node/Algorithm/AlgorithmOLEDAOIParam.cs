using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.Algorithm;

public class AlgorithmOLEDAOIParam : AlgorithmParam
{
	public bool VhLineEnable { get; set; }

	public bool MuraEnable { get; set; }

	public bool PixelDefectEnable { get; set; }

	public string ResultDataFileName { get; set; }

	public AlgorithmOLEDAOIParam(string outputFileName, bool vhLineEnable, bool muraEnable, bool pixelDefectEnable)
	{
		ResultDataFileName = outputFileName;
		VhLineEnable = vhLineEnable;
		MuraEnable = muraEnable;
		PixelDefectEnable = pixelDefectEnable;
	}
}
