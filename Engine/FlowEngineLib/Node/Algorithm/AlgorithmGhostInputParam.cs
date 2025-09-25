using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.Algorithm;

public class AlgorithmGhostInputParam : AlgorithmPreStepParam
{
	public CVTemplateParam TemplateParam { get; set; }

	public string ImgFileName { get; set; }

	public FileExtType FileType { get; set; }

	public int CIE_MasterId { get; set; }

	public int BufferLen { get; set; }

	public SMUResultData SMUData { get; set; }

	public AlgorithmGhostInputParam(int tempId, string tempName, int bufferLen)
	{
		TemplateParam = new CVTemplateParam
		{
			ID = tempId,
			Name = tempName
		};
		FileType = FileExtType.None;
		BufferLen = bufferLen;
	}
}
