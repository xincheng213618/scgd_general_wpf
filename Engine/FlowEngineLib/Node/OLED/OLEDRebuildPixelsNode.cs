using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using FlowEngineLib.Node.Algorithm;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.OLED;

[STNode("/03_5 OLED")]
public class OLEDRebuildPixelsNode : CVBaseServerNodeHub
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(OLEDRebuildPixelsNode));

	private CVOLED_Channel _Channel;

	private string _OutputTemplateName;

	private STNodeEditText<CVOLED_Channel> m_ctrl_channel;

	private STNodeEditText<string> m_ctrl_outtemp;

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

	[STNodeProperty("参数模板", "参数模板", true)]
	[System.ComponentModel.PropertyEditorTypeAttribute(typeof(FlowEngineLib.PropertyEditor.FlowLedCheck2JsonTemplateEditor))]
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

	[STNodeProperty("输出模板", "输出模板", true)]
	[System.ComponentModel.PropertyEditorTypeAttribute(typeof(FlowEngineLib.PropertyEditor.FlowPoiOutputTemplateEditor))]
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
		: base("OLED数据提取", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "OLED.RebuildPixels";
		m_in_text = "IN_IMG";
		m_in_textHub[0] = "IN_IMG";
		m_in_textHub[1] = "IN_POI";
		_OutputTemplateName = string.Empty;
		_Channel = CVOLED_Channel.GREEN;
		_ImgFileName = string.Empty;
		base.Height += 50;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		CreateTempControl(m_custom_item);
		m_custom_item.Y += 25;
		m_ctrl_outtemp = CreateStringControl(m_custom_item, "输出:", _OutputTemplateName);
		m_custom_item.Y += 25;
		m_ctrl_channel = CreateControl(typeof(STNodeEditText<CVOLED_Channel>), m_custom_item, "通道:", _Channel);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmPreStepParam algorithmPreStepParam = new AlgorithmPreStepParam();
		OLEDRebuildPixelsParam oLEDRebuildPixelsParam = new OLEDRebuildPixelsParam(_Channel, _OutputTemplateName);
		getPreStepParam(0, oLEDRebuildPixelsParam);
		getPreStepParam(1, algorithmPreStepParam);
		oLEDRebuildPixelsParam.POI_MasterId = algorithmPreStepParam.MasterId;
		BuildImageParam(oLEDRebuildPixelsParam);
		return oLEDRebuildPixelsParam;
	}
}
