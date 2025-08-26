using FlowEngineLib.Base;

namespace FlowEngineLib.Node.Algorithm;

public class AlgorithmNodeProperty : ILoopNodeProperty
{
	public string AlgorithmType { get; set; }

	public string TempName { get; set; }

	public string ImgFileName { get; set; }

	public string POIName { get; set; }

	public string[] ToItemArray(int no)
	{
		if (!string.IsNullOrEmpty(POIName))
		{
			return new string[5]
			{
				no.ToString(),
				AlgorithmType,
				TempName,
				ImgFileName,
				POIName
			};
		}
		return new string[4]
		{
			no.ToString(),
			AlgorithmType,
			TempName,
			ImgFileName
		};
	}
}
