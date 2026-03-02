using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.Algorithm;

public class AlgorithmImageConvertParam : AlgorithmImageParam
{
	public int Channel { get; set; }

	public ImageFormatType ResultImageFormat { get; set; }

	public string ResultDataFileName { get; set; }

	public AlgorithmImageConvertParam(string outputImgFile, ImageFormatType _ImageFormat, int channel)
	{
		ResultDataFileName = outputImgFile;
		ResultImageFormat = _ImageFormat;
		Channel = channel;
	}
}
