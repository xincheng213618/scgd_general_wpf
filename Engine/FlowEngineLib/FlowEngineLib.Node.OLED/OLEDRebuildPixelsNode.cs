using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.OLED;

[STNode("/03_5 OLED")]
public class OLEDRebuildPixelsNode : CVBaseServerNodeIn2Hub
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(OLEDRebuildPixelsNode));

	private string _TempName;

	private string _ImgFileName;

	private string _OutputTemplateName;

	private STNodeEditText<string> m_ctrl_temp;

	private STNodeEditText<string> m_ctrl_outtemp;

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
			m_ctrl_temp.Value = value;
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

	[STNodeProperty("输出模板", "输出模板", true)]
	public string OutputTemplateName
	{
		get
		{
			return _OutputTemplateName;
		}
		set
		{
			_OutputTemplateName = value;
			m_ctrl_outtemp.Value = value;
		}
	}

	public OLEDRebuildPixelsNode()
		: base("OLED数据提取", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default", 2)
	{
		operatorCode = "OLED.RebuildPixels";
		m_in_text = "IN_IMG";
		m_in2_text = "IN_POI";
		_OutputTemplateName = string.Empty;
		_TempName = string.Empty;
		_ImgFileName = string.Empty;
		base.Height += 25;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_outtemp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "输出:", _TempName);
		m_custom_item.Y += 25;
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "模板:", _TempName);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmPreStepParam algorithmPreStepParam = new AlgorithmPreStepParam();
		OLEDRebuildPixelsParam oLEDRebuildPixelsParam = new OLEDRebuildPixelsParam(_OutputTemplateName, _TempName, _ImgFileName);
		getPreStepParam(masterInput[0], oLEDRebuildPixelsParam);
		getPreStepParam(masterInput[1], algorithmPreStepParam);
		oLEDRebuildPixelsParam.POI_MasterId = algorithmPreStepParam.MasterId;
		if (start.Data.ContainsKey("Image"))
		{
			start.Data.Remove("Image");
		}
		return oLEDRebuildPixelsParam;
	}
}
