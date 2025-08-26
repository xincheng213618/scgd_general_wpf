using System.Drawing;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

public class LVXRCameraNode : CVBaseServerNode
{
	protected int _AvgCount;

	protected float _Gain;

	protected float _ExpTime;

	protected float _Aperture;

	protected bool _EnableFocus;

	protected int _Focus;

	protected string _CaliTempName;

	protected CVImageFlipMode _FlipMode;

	protected string _POITempName;

	protected string _POIFilterTempName;

	protected string _POIReviseTempName;

	private AlgorithmARVRType _Algorithm;

	private string _XRTempName;

	private string operatorCodeXR;

	private STNodeEditText<float> m_ctrl_exp;

	private STNodeEditText<string> m_ctrl_caliTemp;

	private STNodeEditText<string> m_ctrl_poitemplate;

	private STNodeEditText<string> m_ctrl_algTemp;

	private STNodeEditText<AlgorithmARVRType> m_ctrl_algType;

	[STNodeProperty("平均次数", "平均次数", true)]
	public int AvgCount
	{
		get
		{
			return _AvgCount;
		}
		set
		{
			_AvgCount = value;
		}
	}

	[STNodeProperty("增益", "增益", true)]
	public float Gain
	{
		get
		{
			return _Gain;
		}
		set
		{
			_Gain = value;
		}
	}

	[STNodeProperty("曝光时间", "曝光时间", true)]
	public float ExpTime
	{
		get
		{
			return _ExpTime;
		}
		set
		{
			_ExpTime = value;
			m_ctrl_exp.Value = value;
		}
	}

	[STNodeProperty("校正模板", "校正模板", true)]
	public string CaliTempName
	{
		get
		{
			return _CaliTempName;
		}
		set
		{
			_CaliTempName = value;
			m_ctrl_caliTemp.Value = value;
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

	[STNodeProperty("XR类别", "算子XR类别", true)]
	public AlgorithmARVRType Algorithm
	{
		get
		{
			return _Algorithm;
		}
		set
		{
			_Algorithm = value;
			setAlgorithmType();
		}
	}

	[STNodeProperty("XR模板", "XR参数模板", true)]
	public string XRTempName
	{
		get
		{
			return _XRTempName;
		}
		set
		{
			_XRTempName = value;
			m_ctrl_algTemp.Value = _XRTempName;
		}
	}

	public LVXRCameraNode()
		: base("L/BV相机[XR]", "Camera", "SVR.Camera.Default", "DEV.Camera.Default")
	{
		operatorCode = "GetData.XR";
		_FlipMode = CVImageFlipMode.None;
		_ExpTime = 100f;
		_Gain = 10f;
		_CaliTempName = "";
		_POITempName = "";
		_POIFilterTempName = "";
		_POIReviseTempName = "";
		_XRTempName = "";
		operatorCodeXR = "MTF";
		_AvgCount = 1;
		_Aperture = 0f;
		_EnableFocus = false;
		_Focus = 0;
		base.Height += 100;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_exp = CreateControl(typeof(STNodeEditText<float>), m_custom_item, "曝光(ms):", _ExpTime);
		Rectangle custom_item = m_custom_item;
		custom_item.Y += 25;
		m_ctrl_caliTemp = CreateControl(typeof(STNodeEditText<string>), custom_item, "校正:", _CaliTempName);
		custom_item.Y += 25;
		m_ctrl_poitemplate = CreateControl(typeof(STNodeEditText<string>), custom_item, "POI:", GetPOITempDisplay());
		custom_item.Y += 25;
		m_ctrl_algType = CreateControl(typeof(STNodeEditText<AlgorithmARVRType>), custom_item, "XR:", _Algorithm);
		custom_item.Y += 25;
		m_ctrl_algTemp = CreateControl(typeof(STNodeEditText<string>), custom_item, "XR参数:", _XRTempName);
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
		m_ctrl_poitemplate.Value = GetPOITempDisplay();
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		return new LVXRCameraParam(_FlipMode, _EnableFocus, _Focus, _Aperture, _AvgCount, _Gain, new float[1] { _ExpTime }, _CaliTempName, _POITempName, _POIFilterTempName, _POIReviseTempName, string.Empty, operatorCodeXR, _XRTempName);
	}

	protected override int GetMaxDelay()
	{
		return base.GetMaxDelay() + (int)_ExpTime;
	}

	private void setAlgorithmType()
	{
		m_ctrl_algType.Value = _Algorithm;
		AlgorithmObjType.instance.algorithmARVRType = _Algorithm;
		switch (_Algorithm)
		{
		case AlgorithmARVRType.MTF:
			operatorCodeXR = "MTF";
			break;
		case AlgorithmARVRType.SFR:
			operatorCodeXR = "SFR";
			break;
		case AlgorithmARVRType.FOV:
			operatorCodeXR = "FOV";
			break;
		case AlgorithmARVRType.畸变:
			operatorCodeXR = "Distortion";
			break;
		case AlgorithmARVRType.双目融合:
			operatorCodeXR = "ARVR.BinocularFusion";
			break;
		case AlgorithmARVRType.SFR_FindROI:
			operatorCodeXR = "ARVR.SFR.FindROI";
			break;
		case AlgorithmARVRType.十字计算:
			operatorCodeXR = "FindCross";
			break;
		}
		base.nodeEvent?.Invoke(this, new FlowEngineNodeEventArgs());
	}
}
