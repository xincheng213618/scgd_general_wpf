using ColorVision.Engine.PropertyEditor;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;
using System.ComponentModel;

namespace FlowEngineLib;

public class BaseCameraNode : CVBaseServerNode
{
	protected int _AvgCount;

	protected float _Gain;

	protected float _ExpTime;

	protected float _Aperture;

	protected bool _EnableFocus;

	protected int _Focus;

	protected string _CaliTempName;

	protected CVImageFlipMode _FlipMode;

	private STNodeEditText<float> m_ctrl_exp = null!;

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
			OnPropertyChanged();
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
			OnPropertyChanged();
		}
	}

	[STNodeProperty("曝光", "曝光时间", true)]
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
			OnPropertyChanged();
		}
	}

	[STNodeProperty("校正模板", "校正模板", true)]
	[PropertyEditorType(typeof(CalibrationTemplatePropertiesEditor))]
	public string CaliTempName
	{
		get
		{
			return _CaliTempName;
		}
		set
		{
			_CaliTempName = value;
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

	protected BaseCameraNode(string title, string nodeType, string nodeName, string deviceCode)
		: base(title, nodeType, nodeName, deviceCode)
	{
		operatorCode = "GetData";
		_FlipMode = CVImageFlipMode.None;
		_ExpTime = 100f;
		_Gain = 10f;
		_CaliTempName = "";
		_AvgCount = 1;
		_Aperture = 0f;
		_EnableFocus = false;
		_Focus = 0;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_exp = CreateControl(typeof(STNodeEditText<float>), m_custom_item, "曝光:", _ExpTime);
	}

	public override void ApplyCompactNodeDisplay()
	{
		ShowControls = true;
		SetAutoSize(true);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		return new CameraData(_FlipMode, _EnableFocus, _Focus, _Aperture, _AvgCount, _Gain, new float[1] { _ExpTime }, _CaliTempName);
	}

	protected override int GetMaxDelay()
	{
		return base.GetMaxDelay() + (int)_ExpTime;
	}
}
