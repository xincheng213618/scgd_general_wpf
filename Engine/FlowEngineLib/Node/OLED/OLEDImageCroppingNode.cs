using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.OLED;

public class OLEDImageCroppingNode : CVBaseServerNodeHub
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(OLEDImageCroppingNode));

	[STNodeProperty("参数模板", "参数模板", true)]
	[System.ComponentModel.PropertyEditorTypeAttribute(typeof(FlowEngineLib.PropertyEditor.FlowImageCroppingTemplateEditor))]
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
	[System.ComponentModel.DataAnnotations.Display(Order = -100)]
	[System.ComponentModel.PropertyEditorTypeAttribute(typeof(System.ComponentModel.TextSelectFilePropertiesEditor))]
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
		m_in_textHub[0] = "IN_IMG";
		m_in_textHub[1] = "IN_ROI";
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
