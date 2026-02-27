using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

[STNode("/03_3 Image")]
public class AlgorithmImageConvertNode : CVBaseServerNode
{
	private ImageFormatType _ImageFormat;

	private CVOLED_COLOR _Color;

	private string _OutputFileName;

	private STNodeEditText<ImageFormatType> m_ctrl_type;

	private STNodeEditText<CVOLED_COLOR> m_ctrl_color;

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

	[STNodeProperty("颜色", "颜色", true)]
	public CVOLED_COLOR Color
	{
		get
		{
			return _Color;
		}
		set
		{
			_Color = value;
			m_ctrl_color.Value = value;
		}
	}

	public AlgorithmImageConvertNode()
		: base("图像转换", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "Image.Convert";
		_OutputFileName = "";
		_Color = CVOLED_COLOR.GREEN;
		base.Height += 25;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_type = CreateControl(typeof(STNodeEditText<ImageFormatType>), m_custom_item, "图像格式", _ImageFormat);
		m_custom_item.Y += 25;
		m_ctrl_color = CreateControl(typeof(STNodeEditText<CVOLED_COLOR>), m_custom_item, "颜色", _Color);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmImageConvertParam algorithmImageConvertParam = new AlgorithmImageConvertParam(_OutputFileName, _ImageFormat, (int)_Color);
		BuildImageParam(algorithmImageConvertParam);
		getPreStepParam(start, algorithmImageConvertParam);
		return algorithmImageConvertParam;
	}
}
