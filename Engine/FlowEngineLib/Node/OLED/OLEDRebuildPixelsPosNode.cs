using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.OLED;

public class OLEDRebuildPixelsPosNode : CVBaseServerNodeIn2Hub
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(OLEDRebuildPixelsPosNode));

	private string _OutputTemplateName;

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

	public OLEDRebuildPixelsPosNode()
		: base("OLED数据提取2", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "OLED.RebuildPixelsPos";
		m_in_text = "IN_IMG";
		m_in2_text = "IN_POI";
		_OutputTemplateName = string.Empty;
		_ImgFileName = string.Empty;
		base.Height += 25;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_outtemp = CreateStringControl(m_custom_item, "输出:", _OutputTemplateName);
		m_custom_item.Y += 25;
		CreateTempControl(m_custom_item);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmPreStepParam algorithmPreStepParam = new AlgorithmPreStepParam();
		OLEDRebuildPixelsParam oLEDRebuildPixelsParam = new OLEDRebuildPixelsParam(_OutputTemplateName);
		getPreStepParam(0, oLEDRebuildPixelsParam);
		getPreStepParam(1, algorithmPreStepParam);
		oLEDRebuildPixelsParam.POI_MasterId = algorithmPreStepParam.MasterId;
		BuildImageParam(oLEDRebuildPixelsParam);
		return oLEDRebuildPixelsParam;
	}
}
