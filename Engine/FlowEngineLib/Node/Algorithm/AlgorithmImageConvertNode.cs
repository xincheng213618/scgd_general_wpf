using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

[STNode("/03_3 Image")]
public class AlgorithmImageConvertNode : CVBaseServerNode
{
	private ImageFormatType _ImageFormat;

	private CVOLED_Channel _Channel;

	private string _OutputFileName;

	private STNodeEditText<ImageFormatType> m_ctrl_type;

	private STNodeEditText<CVOLED_Channel> m_ctrl_channel;

	[STNodeProperty("图像格式", "图像格式", true)]
	public ImageFormatType ImageFormat
	{
		get
		{
			return _ImageFormat;
		}
		set
		{
			_ImageFormat = value;
			m_ctrl_type.Value = _ImageFormat;
		}
	}

	[STNodeProperty("图像文件", "图像文件", true)]
	public string ImgFileName
	{
		get
		{
			return _ImgFileName;
		}
		set
		{
			_ImgFileName = value;
		}
	}

	[STNodeProperty("通道", "通道", true)]
	public CVOLED_Channel Channel
	{
		get
		{
			return _Channel;
		}
		set
		{
			_Channel = value;
			m_ctrl_channel.Value = value;
		}
	}

	public AlgorithmImageConvertNode()
		: base("图像转换", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "Image.Convert";
		_OutputFileName = "";
		_Channel = CVOLED_Channel.GREEN;
		base.Height += 25;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_type = CreateControl(typeof(STNodeEditText<ImageFormatType>), m_custom_item, "图像格式:", _ImageFormat);
		m_custom_item.Y += 25;
		m_ctrl_channel = CreateControl(typeof(STNodeEditText<CVOLED_Channel>), m_custom_item, "通道:", _Channel);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmImageConvertParam algorithmImageConvertParam = new AlgorithmImageConvertParam(_OutputFileName, _ImageFormat, (int)_Channel);
		BuildImageParam(algorithmImageConvertParam);
		getPreStepParam(start, algorithmImageConvertParam);
		return algorithmImageConvertParam;
	}
}
