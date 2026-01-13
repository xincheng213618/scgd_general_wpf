using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.Algorithm;

public class FindLightAreaParam : AlgorithmImageParam
{
	public CVTemplateParam SavePOITemplate { get; set; }

	public int[] OIndex { get; set; }

	public int BufferLen { get; set; }

	public SMUResultData SMUData { get; set; }

	public FindLightAreaParam(string poiTempName, int[] oIndex)
	{
		SavePOITemplate = new CVTemplateParam
		{
			ID = -1,
			Name = poiTempName
		};
		OIndex = oIndex;
	}
}
