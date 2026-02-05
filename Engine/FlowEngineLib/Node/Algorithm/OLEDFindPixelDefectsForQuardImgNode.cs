using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

public class OLEDFindPixelDefectsForQuardImgNode : CVBaseServerNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(OLEDFindPixelDefectsForQuardImgNode));

	private string _OutputFileName;

	[STNodeProperty("参数模板", "参数模板", true)]
	public string TempName
	{
		get
		{
			return _TempName;
		}
		set
		{
			_TempName = value;
			setTempName(value);
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

	[STNodeProperty("输出文件", "输出文件", true)]
	public string OutputFileName
	{
		get
		{
			return _OutputFileName;
		}
		set
		{
			_OutputFileName = value;
		}
	}

	public OLEDFindPixelDefectsForQuardImgNode()
		: base("AOI亮点检测", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "OLED.FindPixelDefectsForQuardImg";
		_TempName = "";
		_TempId = -1;
		_OutputFileName = "pos.dat";
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		CreateTempControl(m_custom_item);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmOLED_AOIParam algorithmOLED_AOIParam = new AlgorithmOLED_AOIParam(_OutputFileName);
		getPreStepParam(start, algorithmOLED_AOIParam);
		BuildImageParam(algorithmOLED_AOIParam);
		return algorithmOLED_AOIParam;
	}
}
