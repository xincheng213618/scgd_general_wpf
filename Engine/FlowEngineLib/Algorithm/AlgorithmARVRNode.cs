using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Algorithm;

[STNode("/03_2 Algorithm")]
public class AlgorithmARVRNode : CVBaseServerNode
{
	private AlgorithmARVRType _Algorithm;

	private string _POITempName;

	private CVOLED_COLOR _Color;

	private int _BufferLen;

	private STNodeEditText<AlgorithmARVRType> m_ctrl_editText;

	private STNodeEditText<CVOLED_COLOR> m_ctrl_color;

	[STNodeProperty("算子", "算子", true)]
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

	[STNodeProperty("参数模板", "参数模板", true)]
	public string TempName
	{
		get
		{
			return _TempName;
		}
		set
		{
			_TempName = value;
			setTempName(value);
		}
	}

	[STNodeProperty("POI模板", "POI模板名称", true)]
	public string POITempName
	{
		get
		{
			return _POITempName;
		}
		set
		{
			_POITempName = value;
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

	[STNodeProperty("颜色", "颜色", true)]
	public CVOLED_COLOR Color
	{
		get
		{
			return _Color;
		}
		set
		{
			_Color = value;
			m_ctrl_color.Value = value;
		}
	}

	[STNodeProperty("缓存大小", "缓存大小", true)]
	public int BufferLen
	{
		get
		{
			return _BufferLen;
		}
		set
		{
			_BufferLen = value;
		}
	}

	public AlgorithmARVRNode()
		: base("ARVR算法", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "MTF";
		base.Height += 50;
		_BufferLen = 1024;
		_Color = CVOLED_COLOR.GREEN;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_editText = CreateControl(typeof(STNodeEditText<AlgorithmARVRType>), m_custom_item, "算子:", _Algorithm);
		m_custom_item.Y += 25;
		CreateTempControl(m_custom_item);
		m_custom_item.Y += 25;
		m_ctrl_color = CreateControl(typeof(STNodeEditText<CVOLED_COLOR>), m_custom_item, "颜色:", _Color);
	}

	private void setAlgorithmType()
	{
		m_ctrl_editText.Value = _Algorithm;
		AlgorithmObjType.instance.algorithmARVRType = _Algorithm;
		switch (_Algorithm)
		{
		case AlgorithmARVRType.MTF:
			operatorCode = "MTF";
			break;
		case AlgorithmARVRType.SFR:
			operatorCode = "SFR";
			break;
		case AlgorithmARVRType.FOV:
			operatorCode = "FOV";
			break;
		case AlgorithmARVRType.畸变:
			operatorCode = "Distortion";
			break;
		case AlgorithmARVRType.双目融合:
			operatorCode = "ARVR.BinocularFusion";
			break;
		case AlgorithmARVRType.SFR_FindROI:
			operatorCode = "ARVR.SFR.FindROI";
			break;
		case AlgorithmARVRType.十字计算:
			operatorCode = "FindCross";
			break;
		}
		base.nodeEvent?.Invoke(this, new FlowEngineNodeEventArgs());
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmParam_ROI algorithmParam_ROI = new AlgorithmParam_ROI();
		algorithmParam_ROI.POITemplateParam = new CVTemplateParam
		{
			ID = -1,
			Name = _POITempName
		};
		algorithmParam_ROI.IsInversion = false;
		algorithmParam_ROI.BufferLen = _BufferLen;
		getPreStepParam(start, algorithmParam_ROI);
		BuildImageParam(_Color, algorithmParam_ROI);
		algorithmParam_ROI.SMUData = GetSMUResult(start);
		return algorithmParam_ROI;
	}
}
