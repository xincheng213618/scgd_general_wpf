using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.OLED;

public class AlgorithmCompoundImgParam
{
	public int OrderIndex { get; set; }

	public CVTemplateParam TemplateParam { get; set; }

	public AlgorithmPreStepParam IMG1_Master { get; set; }

	public AlgorithmPreStepParam IMG2_Master { get; set; }

	public string ResultDataFileName { get; set; }

	public int BufferLen { get; set; }

	public AlgorithmCompoundImgParam(string tempName, AlgorithmPreStepParam img1_mid, AlgorithmPreStepParam img2_mid, int orderIndex, int bufferLen, string outputImgFile)
	{
		IMG1_Master = img1_mid;
		IMG2_Master = img2_mid;
		TemplateParam = new CVTemplateParam
		{
			ID = -1,
			Name = tempName
		};
		OrderIndex = orderIndex;
		BufferLen = bufferLen;
		ResultDataFileName = outputImgFile;
	}
}
