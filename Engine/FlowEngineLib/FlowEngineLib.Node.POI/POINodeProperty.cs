using FlowEngineLib.Base;

namespace FlowEngineLib.Node.POI;

public class POINodeProperty : ILoopNodeProperty
{
	public string TempName { get; set; }

	public string FilterTempName { get; set; }

	public string ReviseTempName { get; set; }

	public string OutputTempName { get; set; }

	public string ImgFileName { get; set; }

	public POIStorageModel POIOutput { get; set; }

	public string OutputFileName { get; set; }

	public string[] ToItemArray(int no)
	{
		return new string[6]
		{
			no.ToString(),
			TempName,
			FilterTempName,
			ReviseTempName,
			OutputTempName,
			ImgFileName
		};
	}
}
