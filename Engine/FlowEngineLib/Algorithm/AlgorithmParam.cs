namespace FlowEngineLib.Algorithm;

public class AlgorithmParam : AlgorithmPreStepParam
{
	public string ImgFileName { get; set; }

	public FileExtType FileType { get; set; }

	public CVOLED_COLOR Color { get; set; }

	public CVTemplateParam TemplateParam { get; set; }

	public string OutputFileName { get; set; }

	public bool IsInversion { get; set; }

	public int BufferLen { get; set; }

	public SMUResultData SMUData { get; set; }

	public AlgorithmParam()
	{
		FileType = FileExtType.None;
		TemplateParam = new CVTemplateParam
		{
			ID = -1
		};
		SMUData = null;
	}

	public AlgorithmParam(string imgFile, int tempId, string tempName, AlgorithmPreStepParam param, string globalVariableName)
		: base(param)
	{
		ImgFileName = imgFile;
		FileType = FileExtType.None;
		TemplateParam = new CVTemplateParam
		{
			ID = tempId,
			Name = tempName
		};
		SMUData = null;
	}
}
