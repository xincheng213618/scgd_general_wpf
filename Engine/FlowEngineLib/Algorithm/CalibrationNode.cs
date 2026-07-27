using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Algorithm;

[STNode("/03_3 校正")]
public class CalibrationNode : CVBaseServerNode
{
	private string _ExpTempName;

	protected bool _IsSaveCIE;

	private string _POITempName;

	protected string _POIFilterTempName;

	protected string _POIReviseTempName;

	private string _OutputTemplateName;

	private STNodeEditText<string> m_ctrl_temp_exp;

	private STNodeEditText<string> m_ctrl_temp_poi;

	private STNodeEditText<string> m_ctrl_outtemp;

	private STNodeEditText<bool> m_ctrl_saveCIE;

	[STNodeProperty("参数模板", "参数模板", true)]
	[System.ComponentModel.PropertyEditorTypeAttribute(typeof(FlowEngineLib.PropertyEditor.FlowCalibrationTemplateEditor))]
	public string TempName
	{
		get
		{
			return _TempName;
		}
		set
		{
			setTempName(value);
			OnPropertyChanged();
		}
	}

	[STNodeProperty("曝光模板", "曝光模板", true)]
	[System.ComponentModel.PropertyEditorTypeAttribute(typeof(FlowEngineLib.PropertyEditor.FlowAutoExposureTemplateEditor))]
	public string ExpTempName
	{
		get
		{
			return _ExpTempName;
		}
		set
		{
			_ExpTempName = value;
			m_ctrl_temp_exp.Value = value;
			OnPropertyChanged();
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
			OnPropertyChanged();
		}
	}

	[STNodeProperty("保存CIE文件", "保存CIE文件", true)]
	public bool IsSaveCIE
	{
		get
		{
			return _IsSaveCIE;
		}
		set
		{
			_IsSaveCIE = value;
			m_ctrl_saveCIE.Value = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("POI模板", "POI算法模板", true)]
	[System.ComponentModel.PropertyEditorTypeAttribute(typeof(FlowEngineLib.PropertyEditor.FlowPoiTemplateEditor))]
	public string POITempName
	{
		get
		{
			return _POITempName;
		}
		set
		{
			_POITempName = value;
			setPOITemp();
			OnPropertyChanged();
		}
	}

	[STNodeProperty("POI过滤", "POI过滤模板", true)]
	[System.ComponentModel.PropertyEditorTypeAttribute(typeof(FlowEngineLib.PropertyEditor.FlowPoiFilterTemplateEditor))]
	public string POIFilterTempName
	{
		get
		{
			return _POIFilterTempName;
		}
		set
		{
			_POIFilterTempName = value;
			setPOITemp();
			OnPropertyChanged();
		}
	}

	[STNodeProperty("POI修正", "POI修正模板", true)]
	[System.ComponentModel.PropertyEditorTypeAttribute(typeof(FlowEngineLib.PropertyEditor.FlowPoiReviseTemplateEditor))]
	public string POIReviseTempName
	{
		get
		{
			return _POIReviseTempName;
		}
		set
		{
			_POIReviseTempName = value;
			setPOITemp();
			OnPropertyChanged();
		}
	}

	[STNodeProperty("文件输出模板", "文件输出模板", true)]
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
			OnPropertyChanged();
		}
	}

	public CalibrationNode()
		: base("校正", "Calibration", "SVR.Calibration.Default", "DEV.Calibration.Default")
	{
		operatorCode = "Calibration";
		_ExpTempName = "";
		_POITempName = "";
		_POIFilterTempName = "";
		_POIReviseTempName = "";
		_OutputTemplateName = "";
		base.Height += 100;
		_MaxTime = 10000;
		_IsSaveCIE = true;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		CreateTempControl(m_custom_item, "校正模板:");
		m_custom_item.Y += 25;
		m_ctrl_temp_exp = CreateStringControl(m_custom_item, "曝光模板:", _ExpTempName);
		m_custom_item.Y += 25;
		m_ctrl_saveCIE = CreateControl(typeof(STNodeEditText<bool>), m_custom_item, "保存CIE文件:", _IsSaveCIE);
		m_custom_item.Y += 25;
		m_ctrl_temp_poi = CreateStringControl(m_custom_item, "POI模板:", _POITempName);
		m_custom_item.Y += 25;
		m_ctrl_outtemp = CreateStringControl(m_custom_item, "文件输出模板:", _OutputTemplateName);
	}

	private void setPOITemp()
	{
		m_ctrl_temp_poi.Value = GetPOITempDisplay();
	}

	private string GetPOITempDisplay()
	{
		if (string.IsNullOrEmpty(_POITempName))
		{
			return string.Empty;
		}
		return $"{_POITempName}/{_POIFilterTempName}/{_POIReviseTempName}";
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmPreStepParam param = new AlgorithmPreStepParam();
		getPreStepParam(start, param);
		CalibrationData calibrationData = new CalibrationData(_ExpTempName, param, _IsSaveCIE);
		BuildImageParam(calibrationData);
		if (!string.IsNullOrEmpty(_POITempName))
		{
			POITemplateParam pOIParam = new POITemplateParam(_POITempName, _POIFilterTempName, _POIReviseTempName);
			calibrationData.POIParam = pOIParam;
		}
		return calibrationData;
	}
}
