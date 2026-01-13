using System.Drawing;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Camera;

[STNode("/02 相机")]
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

	private STNodeEditText<string> m_ctrl_caliTemp;

	private STNodeEditText<string> m_ctrl_camTemp;

	private STNodeEditText<string> m_ctrl_poiTemp;

	private STNodeEditText<string> m_ctrl_expAutoTemp;

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
			setTempValue();
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
			setTempValue();
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
			m_ctrl_expAutoTemp.Value = GetAutoExpDis();
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
			m_ctrl_expAutoTemp.Value = GetAutoExpDis();
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
			m_ctrl_caliTemp.Value = value;
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
			setPOITemp();
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
			setPOITemp();
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
			setPOITemp();
		}
	}

	private void setTempValue()
	{
		m_ctrl_camTemp.Value = GetCameraTempDis();
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
		base.Height += 75;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		initCtrl();
	}

	private string GetCameraTempDis()
	{
		return string.Format("{0}:{1}", _IsHDR ? "HDR" : "Nor", _CamTempName);
	}

	private void initCtrl()
	{
		Rectangle custom_item = m_custom_item;
		m_ctrl_expAutoTemp = CreateControl(typeof(STNodeEditText<string>), custom_item, "自动曝光/ND:", GetAutoExpDis());
		custom_item.Y += 25;
		m_ctrl_camTemp = CreateControl(typeof(STNodeEditText<string>), custom_item, "相机:", GetCameraTempDis());
		custom_item.Y += 25;
		m_ctrl_caliTemp = CreateControl(typeof(STNodeEditText<string>), custom_item, "校正:", _CalibTempName);
		custom_item.Y += 25;
		m_ctrl_poiTemp = CreateControl(typeof(STNodeEditText<string>), custom_item, "POI:", _POITempName);
	}

	private string GetAutoExpDis()
	{
		return string.Format("{0}/{1}", _IsAutoExp ? "T" : "F", _IsWithND ? "T" : "F");
	}

	private string GetPOITempDisplay()
	{
		if (string.IsNullOrEmpty(_POITempName))
		{
			return string.Empty;
		}
		return $"{_POITempName}/{_POIFilterTempName}/{_POIReviseTempName}";
	}

	private void setPOITemp()
	{
		m_ctrl_poiTemp.Value = GetPOITempDisplay();
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		return new CommCameraData(_CamTempName, _IsWithND, _IsAutoExp, _TempName, _CalibTempName, _POITempName, _POIFilterTempName, _POIReviseTempName, _GlobalVariableName, _IsHDR);
	}
}
