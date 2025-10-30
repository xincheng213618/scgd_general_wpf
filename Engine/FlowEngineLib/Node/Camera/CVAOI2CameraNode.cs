using System.Drawing;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using ST.Library.UI;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Camera;

[STNode("/02 相机")]
public class CVAOI2CameraNode : CVBaseServerNodeIn2Hub
{
	protected bool _IsHDR;

	private string _CamTempName;

	private bool _IsSaveRawImg;

	protected CVImageFlipMode _FlipMode;

	protected bool _IsAutoExp;

	private string _TempName;

	private bool _IsWithND;

	protected bool _IsAutoFocus;

	private string _FocusTempName;

	protected string _CalibTempName;

	private AOI2TypeEnum _AOIType;

	protected string _AlgTempName;

	private STNodeEditText<string> m_ctrl_algTemp;

	private STNodeEditText<string> m_ctrl_camTemp;

	private STNodeEditText<string> m_ctrl_caliTemp;

	private STNodeEditText<string> m_ctrl_expAutoTemp;

	private STNodeEditText<string> m_ctrl_img;

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

	[STNodeProperty("保存原图", "是否保存原图", true)]
	public bool IsSaveRawImg
	{
		get
		{
			return _IsSaveRawImg;
		}
		set
		{
			_IsSaveRawImg = value;
			setImgValue();
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
			setImgValue();
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

	[STNodeProperty("AOI算法", "AOI算法", true)]
	public AOI2TypeEnum AOIType
	{
		get
		{
			return _AOIType;
		}
		set
		{
			_AOIType = value;
			setAOIValue();
		}
	}

	[STNodeProperty("算子模板", "算子模板", true)]
	public string AlgTempName
	{
		get
		{
			return _AlgTempName;
		}
		set
		{
			_AlgTempName = value;
			setAOIValue();
		}
	}

	private void setTempValue()
	{
		m_ctrl_camTemp.Value = GetCameraTempDis();
	}

	public CVAOI2CameraNode()
		: base("通用AOI相机2", "Camera", "SVR.Camera.Default", "DEV.Camera.Default")
	{
		operatorCode = "GetDataAndAlgorithm";
		m_in_text = "IN_IMG";
		m_in2_text = "IN_POI";
		_MaxTime = 60000;
		_AlgTempName = "";
		_CamTempName = "";
		_TempName = "";
		_CalibTempName = "";
		_FlipMode = CVImageFlipMode.None;
		_IsWithND = false;
		_IsAutoExp = false;
		_IsAutoFocus = false;
		_IsSaveRawImg = false;
		_FocusTempName = string.Empty;
		base.Width = 180;
		m_custom_item.Width += 30;
		base.Height += 100;
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
		m_ctrl_camTemp = CreateControl(typeof(STNodeEditText<string>), custom_item, "相机:", GetCameraTempDis());
		custom_item.Y += 25;
		m_ctrl_expAutoTemp = CreateControl(typeof(STNodeEditText<string>), custom_item, "自动曝光/ND:", GetAutoExpDis());
		custom_item.Y += 25;
		m_ctrl_img = CreateControl(typeof(STNodeEditText<string>), custom_item, "保存/翻转:", GetImgTempDis());
		custom_item.Y += 25;
		m_ctrl_caliTemp = CreateControl(typeof(STNodeEditText<string>), custom_item, "校正:", _CalibTempName);
		custom_item.Y += 25;
		m_ctrl_algTemp = CreateControl(typeof(STNodeEditText<string>), custom_item, "算子:", GetAlgTempDis());
	}

	private string GetAutoExpDis()
	{
		return string.Format("{0}/{1}", _IsAutoExp ? "T" : "F", _IsWithND ? "T" : "F");
	}

	private void setImgValue()
	{
		m_ctrl_img.Value = GetImgTempDis();
	}

	private void setAOIValue()
	{
		m_ctrl_algTemp.Value = GetAlgTempDis();
	}

	private string GetImgTempDis()
	{
		return string.Format("{0}/{1}", _IsSaveRawImg ? "T" : "F", _FlipMode.ToString());
	}

	private string GetAlgTempDis()
	{
		return $"{Lang.Get(_AOIType.ToString())}:{_AlgTempName}";
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmPreStepParam algorithmPreStepParam = new AlgorithmPreStepParam();
		getPreStepParam(1, algorithmPreStepParam);
		string algParamType = "OLED_RebuildPixelsMem";
		return new CVAOI2CameraParam(_CamTempName, _IsWithND, _IsAutoExp, _TempName, _CalibTempName, algParamType, _AlgTempName, algorithmPreStepParam.MasterId, _IsHDR, _IsSaveRawImg);
	}
}
