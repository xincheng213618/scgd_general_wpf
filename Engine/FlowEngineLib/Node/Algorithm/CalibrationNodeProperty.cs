using FlowEngineLib.Base;

namespace FlowEngineLib.Node.Algorithm;

public class CalibrationNodeProperty : ILoopNodeProperty
{
	public string TempName { get; set; }

	public string ExpTempName { get; set; }

	public string ImgFileName { get; set; }

	public string[] ToItemArray(int no)
	{
		return new string[4]
		{
			no.ToString(),
			TempName,
			ExpTempName,
			ImgFileName
		};
	}
}
