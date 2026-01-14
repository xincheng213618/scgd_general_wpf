using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.Algorithm;

public class AlgorithmGhostInputParam : AlgorithmImageParam
{
	public int CIE_MasterId { get; set; }

	public int BufferLen { get; set; }

	public SMUResultData SMUData { get; set; }

	public AlgorithmGhostInputParam(int bufferLen)
	{
		base.FileType = FileExtType.None;
		BufferLen = bufferLen;
	}
}
