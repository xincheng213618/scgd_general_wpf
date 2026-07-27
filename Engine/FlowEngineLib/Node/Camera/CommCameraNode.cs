using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Camera;

[STNode("/02 相机")]
[FlowEngineLib.PropertyEditor.FlowNodePropertyEditorAttribute("CalibTempName", typeof(FlowEngineLib.PropertyEditor.FlowCalibrationTemplateEditor))]
[FlowEngineLib.PropertyEditor.FlowNodePropertyEditorAttribute("POITempName", typeof(FlowEngineLib.PropertyEditor.FlowPoiTemplateEditor))]
[FlowEngineLib.PropertyEditor.FlowNodePropertyEditorAttribute("POIFilterTempName", typeof(FlowEngineLib.PropertyEditor.FlowPoiFilterTemplateEditor))]
[FlowEngineLib.PropertyEditor.FlowNodePropertyEditorAttribute("POIReviseTempName", typeof(FlowEngineLib.PropertyEditor.FlowPoiReviseTemplateEditor))]
public class CommCameraNode : CVBaseServerNode
{
	protected string _GlobalVariableName;

	protected bool _IsHDR;

	private string _CamTempName;

	protected CVImageFlipMode _FlipMode;

	protected bool _IsAutoExp;

	private bool _IsWithND;

	protected bool _IsAutoFocus;

	private string _FocusTempName;

	protected string _CalibTempName;

	private string _POITempName;

	protected string _POIFilterTempName;

	protected string _POIReviseTempName;

	private STNodeEditText<string> m_ctrl_camTemp;

	[STNodeProperty("HDR", "HDR", true)]
	public bool IsHDR
	{
		get
		{
			return _IsHDR;
		}
		set
		{
			_IsHDR = value;
			CamTempName = string.Empty;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("相机模板", "相机参数模板", true)]
	public string CamTempName
	{
		get
		{
			return _CamTempName;
		}
		set
		{
			_CamTempName = value;
			if (m_ctrl_camTemp != null)
			{
				m_ctrl_camTemp.Value = value;
			}
			OnPropertyChanged();
		}
	}

	[STNodeProperty("图像翻转", "图像翻转", true)]
	public CVImageFlipMode FlipMode
	{
		get
		{
			return _FlipMode;
		}
		set
		{
			_FlipMode = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("自动曝光", "自动曝光", true)]
	public bool IsAutoExp
	{
		get
		{
			return _IsAutoExp;
		}
		set
		{
			_IsAutoExp = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("曝光模板", "曝光模板", true)]
	public string TempName
	{
		get
		{
			return _TempName;
		}
		set
		{
			_TempName = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("启用ND", "启用ND滤轮(自动曝光)", true)]
	public bool IsWithND
	{
		get
		{
			return _IsWithND;
		}
		set
		{
			_IsWithND = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("校正模板", "校正模板", true)]
	public string CalibTempName
	{
		get
		{
			return _CalibTempName;
		}
		set
		{
			_CalibTempName = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("POI模板", "POI算法模板", true)]
	public string POITempName
	{
		get
		{
			return _POITempName;
		}
		set
		{
			_POITempName = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("POI过滤", "POI过滤模板", true)]
	public string POIFilterTempName
	{
		get
		{
			return _POIFilterTempName;
		}
		set
		{
			_POIFilterTempName = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("POI修正", "POI修正模板", true)]
	public string POIReviseTempName
	{
		get
		{
			return _POIReviseTempName;
		}
		set
		{
			_POIReviseTempName = value;
			OnPropertyChanged();
		}
	}

	public CommCameraNode()
		: base("通用相机", "Camera", "SVR.Camera.Default", "DEV.Camera.Default")
	{
		operatorCode = "GetData";
		_MaxTime = 60000;
		_CalibTempName = "";
		_CamTempName = "";
		_TempName = "";
		_POITempName = "";
		_POIFilterTempName = "";
		_FlipMode = CVImageFlipMode.None;
		_IsAutoFocus = false;
		_FocusTempName = string.Empty;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		initCtrl();
	}

	public override void ApplyCompactNodeDisplay()
	{
		ShowControls = true;
		SetAutoSize(true);
	}

	private void initCtrl()
	{
		m_ctrl_camTemp = CreateStringControl(m_custom_item, "", _CamTempName);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		return new CommCameraData(_CamTempName, _IsWithND, _IsAutoExp, _TempName, _CalibTempName, _POITempName, _POIFilterTempName, _POIReviseTempName, _GlobalVariableName, _IsHDR, _FlipMode);
	}
}
