namespace FlowEngineLib.Algorithm;

public class AlgorithmImageParam : AlgorithmBaseParam
{
	public CVOLED_COLOR Color { get; set; }

	public string ImgFileName { get; set; }

	public FileExtType FileType { get; set; }

	public AlgorithmImageParam()
	{
		Color = CVOLED_COLOR.GREEN;
		FileType = FileExtType.None;
	}

	public AlgorithmImageParam(AlgorithmPreStepParam param)
		: base(param)
	{
		Color = CVOLED_COLOR.GREEN;
		FileType = FileExtType.None;
	}
}
