using System.Drawing;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using FlowEngineLib.Camera;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

[STNode("/02 相机")]
[FlowEngineLib.PropertyEditor.FlowNodePropertyEditorAttribute("CalibTempName", typeof(FlowEngineLib.PropertyEditor.FlowCalibrationTemplateEditor))]
[FlowEngineLib.PropertyEditor.FlowNodePropertyEditorAttribute("POITempName", typeof(FlowEngineLib.PropertyEditor.FlowPoiTemplateEditor))]
[FlowEngineLib.PropertyEditor.FlowNodePropertyEditorAttribute("POIFilterTempName", typeof(FlowEngineLib.PropertyEditor.FlowPoiFilterTemplateEditor))]
[FlowEngineLib.PropertyEditor.FlowNodePropertyEditorAttribute("POIReviseTempName", typeof(FlowEngineLib.PropertyEditor.FlowPoiReviseTemplateEditor))]
public class CVCameraNode : CVBaseServerNode
{
	protected int _AvgCount;

	protected float _Gain;

	protected float _TempR;

	protected float _TempG;

	protected float _TempB;

	protected CV2LVChannelMode _CV2LVChannel;

	protected float _Aperture;

	protected bool _EnableFocus;

	protected int _Focus;

	protected string _CalibTempName;

	protected CVImageFlipMode _FlipMode;

	private string _POITempName;

	protected string _POIFilterTempName;

	protected string _POIReviseTempName;

	private STNodeEditText<float>[] m_ctrl_expTime;

	private STNodeEditText<string> m_ctrl_caliTemp;

	private STNodeEditText<string> m_ctrl_poitemplate;

	private STNodeEditText<CV2LVChannelMode> m_ctrl_CV2LVChannel;

	private Channel _Channel;

	private string[] szTypeCode = new string[3] { "R", "G", "B" };

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

	[STNodeProperty("R曝光", "RExpDesc", true)]
	public float TempR
	{
		get
		{
			return _TempR;
		}
		set
		{
			_TempR = value;
			m_ctrl_expTime[0].Value = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("G曝光", "GExpDesc", true)]
	public float TempG
	{
		get
		{
			return _TempG;
		}
		set
		{
			_TempG = value;
			m_ctrl_expTime[1].Value = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("B曝光", "BExpDesc", true)]
	public float TempB
	{
		get
		{
			return _TempB;
		}
		set
		{
			_TempB = value;
			m_ctrl_expTime[2].Value = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("CV2LVChannel", "CV2LVChannel", true)]
	public CV2LVChannelMode CV2LVChannel
	{
		get
		{
			return _CV2LVChannel;
		}
		set
		{
			_CV2LVChannel = value;
			m_ctrl_CV2LVChannel.Value = value;
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
			m_ctrl_caliTemp.Value = value;
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
			setPOITemp();
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
			setPOITemp();
			OnPropertyChanged();
		}
	}

	public CVCameraNode()
		: base("CV相机", "Camera", "SVR.Camera.Default", "DEV.Camera.Default")
	{
		operatorCode = "GetData";
		_FlipMode = CVImageFlipMode.None;
		_CV2LVChannel = CV2LVChannelMode.None;
		_MaxTime = 20000;
		_CalibTempName = "";
		_POITempName = "";
		_POIFilterTempName = "";
		_POIReviseTempName = "";
		_TempR = 100f;
		_TempG = 100f;
		_TempB = 100f;
		_Gain = 10f;
		_AvgCount = 1;
		_Aperture = 0f;
		_EnableFocus = false;
		_Focus = 0;
		base.Height += 125;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		float[] array = new float[3] { _TempR, _TempG, _TempB };
		_Channel = new Channel();
		for (int i = 0; i < szTypeCode.Length; i++)
		{
			_Channel.Add(szTypeCode[i], i, array[i]);
		}
		initCtrl();
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

	protected override int GetMaxDelay()
	{
		return base.GetMaxDelay() + (int)(_TempR + _TempG + _TempB);
	}

	private void initCtrl()
	{
		int channelCount = _Channel.GetChannelCount();
		m_ctrl_expTime = new STNodeEditText<float>[channelCount];
		Rectangle custom_item = m_custom_item;
		for (int i = 0; i < channelCount; i++)
		{
			ChannelData channel = _Channel.GetChannel(i);
			m_ctrl_expTime[i] = CreateControl(typeof(STNodeEditText<float>), custom_item, szTypeCode[i] + "曝光:", channel.Temp);
			custom_item.Y += 25;
		}
		m_ctrl_CV2LVChannel = CreateControl(typeof(STNodeEditText<CV2LVChannelMode>), custom_item, "CV2LVChannel:", _CV2LVChannel);
		custom_item.Y += 25;
		m_ctrl_caliTemp = CreateStringControl(custom_item, "校正模板:", _CalibTempName);
		custom_item.Y += 25;
		m_ctrl_poitemplate = CreateStringControl(custom_item, "POI:", GetPOITempDisplay());
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		float[] expTime = new float[3]
		{
			m_ctrl_expTime[0].Value,
			m_ctrl_expTime[1].Value,
			m_ctrl_expTime[2].Value
		};
		return new CVCameraData(_FlipMode, _CV2LVChannel, _EnableFocus, _Focus, _Aperture, _AvgCount, _Gain, expTime, _CalibTempName, _POITempName, _POIFilterTempName, _POIReviseTempName);
	}
}
