using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.Algorithm;

public class OLEDCombineQuaterImagesParams : AlgorithmParam
{
	public string[] InputImageFiles { get; set; }

	public int[] InputImages_MasterId { get; set; }

	public OLEDCombineQuaterImagesParams(CVOLED_COLOR color, string _ImgFileName1, string _ImgFileName2, string _ImgFileName3, string _ImgFileName4, string outputFileName)
	{
		base.Color = color;
		InputImageFiles = new string[4];
		InputImageFiles[0] = _ImgFileName1;
		InputImageFiles[1] = _ImgFileName2;
		InputImageFiles[2] = _ImgFileName3;
		InputImageFiles[3] = _ImgFileName4;
		base.OutputFileName = outputFileName;
		InputImages_MasterId = new int[4];
	}
}
