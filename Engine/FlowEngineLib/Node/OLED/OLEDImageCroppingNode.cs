using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.OLED;

public class OLEDImageCroppingNode : CVBaseServerNodeIn2Hub
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(OLEDImageCroppingNode));

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

	public OLEDImageCroppingNode()
		: base("图像裁剪2", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		base.Height = 90;
		operatorCode = "OLED.GetRIAand";
		m_in_text = "IN_IMG";
		m_in2_text = "IN_ROI";
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		CreateTempControl(m_custom_item);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmPreStepParam algorithmPreStepParam = new AlgorithmPreStepParam();
		OLEDImageCroppingParam oLEDImageCroppingParam = new OLEDImageCroppingParam();
		getPreStepParam(0, oLEDImageCroppingParam);
		getPreStepParam(1, algorithmPreStepParam);
		oLEDImageCroppingParam.ROI_MasterId = algorithmPreStepParam.MasterId;
		BuildImageParam(oLEDImageCroppingParam);
		return oLEDImageCroppingParam;
	}
}
