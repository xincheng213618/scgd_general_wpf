using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.Algorithm;

public class AlgorithmOLEDAOIParam : AlgorithmParam
{
	public bool VhLineEnable { get; set; }

	public bool MuraEnable { get; set; }

	public bool PixelDefectEnable { get; set; }

	public string ResultDataFileName { get; set; }

	public string CustomSN { get; set; }

	public AlgorithmOLEDAOIParam(string customSN, string outputFileName, bool vhLineEnable, bool muraEnable, bool pixelDefectEnable)
	{
		CustomSN = customSN;
		ResultDataFileName = outputFileName;
		VhLineEnable = vhLineEnable;
		MuraEnable = muraEnable;
		PixelDefectEnable = pixelDefectEnable;
	}
}
