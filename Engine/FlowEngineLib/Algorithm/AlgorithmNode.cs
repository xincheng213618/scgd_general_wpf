using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Algorithm;

[STNode("/03_2 Algorithm")]
public class AlgorithmNode : CVBaseServerNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(AlgorithmNode));

	private AlgorithmType _Algorithm;

	private string _POITempName;

	private int _POITempId;

	private CVOLED_COLOR _Color;

	private bool _IsInversion;

	private int _BufferLen;

	private STNodeEditText<AlgorithmType> m_ctrl_editText;

	private STNodeEditText<CVOLED_COLOR> m_ctrl_color;

	[STNodeProperty("算子", "算子", true)]
	public AlgorithmType Algorithm
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
			setTempName(value);
		}
	}

	[STNodeProperty("POI模板", "POI模板", true)]
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

	public AlgorithmNode()
		: base("AI算法", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "MTF";
		_TempName = "";
		_TempId = -1;
		_POITempId = -1;
		_BufferLen = 1024;
		_IsInversion = false;
		_Color = CVOLED_COLOR.GREEN;
		base.Height += 50;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_editText = CreateControl(typeof(STNodeEditText<AlgorithmType>), m_custom_item, "算子:", _Algorithm);
		m_custom_item.Y += 25;
		CreateTempControl(m_custom_item);
		m_custom_item.Y += 25;
		m_ctrl_color = CreateControl(typeof(STNodeEditText<CVOLED_COLOR>), m_custom_item, "颜色:", _Color);
	}

	private void setAlgorithmType()
	{
		m_ctrl_editText.Value = _Algorithm;
		AlgorithmObjType.instance.algorithmType = _Algorithm;
		switch (_Algorithm)
		{
		case AlgorithmType.MTF:
			operatorCode = "MTF";
			break;
		case AlgorithmType.SFR:
			operatorCode = "SFR";
			break;
		case AlgorithmType.FOV:
			operatorCode = "FOV";
			break;
		case AlgorithmType.鬼影:
			operatorCode = "Ghost";
			break;
		case AlgorithmType.畸变:
			operatorCode = "Distortion";
			break;
		case AlgorithmType.灯珠检测:
			operatorCode = "LedCheck";
			break;
		case AlgorithmType.发光区检测:
			operatorCode = "FocusPoints";
			break;
		case AlgorithmType.发光区检测OLED:
			operatorCode = "OLED.GetRIAandPT";
			break;
		case AlgorithmType.灯带检测:
			operatorCode = "LEDStripDetection";
			break;
		case AlgorithmType.JND:
			operatorCode = "OLED.JND.CalVas";
			break;
		case AlgorithmType.图像裁剪:
			operatorCode = "OLED.GetRIAand";
			break;
		case AlgorithmType.双目融合:
			operatorCode = "ARVR.BinocularFusion";
			break;
		case AlgorithmType.SFR_FindROI:
			operatorCode = "ARVR.SFR.FindROI";
			break;
		case AlgorithmType.AA布点:
			operatorCode = "ARVR.AA.FindPoints";
			break;
		case AlgorithmType.ImageCompound:
			operatorCode = "CompoundImg";
			break;
		case AlgorithmType.十字计算:
			operatorCode = "FindCross";
			break;
		}
		base.nodeEvent?.Invoke(this, new FlowEngineNodeEventArgs());
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmParam algorithmParam = null;
		switch (_Algorithm)
		{
		case AlgorithmType.MTF:
		case AlgorithmType.SFR:
		case AlgorithmType.灯珠检测:
		case AlgorithmType.灯带检测:
		case AlgorithmType.发光区检测:
		case AlgorithmType.JND:
		case AlgorithmType.图像裁剪:
		case AlgorithmType.SFR_FindROI:
		case AlgorithmType.十字计算:
			algorithmParam = new AlgorithmParam_ROI
			{
				POITemplateParam = new CVTemplateParam
				{
					ID = _POITempId,
					Name = _POITempName
				}
			};
			break;
		default:
			algorithmParam = new AlgorithmParam();
			break;
		}
		algorithmParam.IsInversion = _IsInversion;
		algorithmParam.BufferLen = _BufferLen;
		BuildImageParam(_ImgFileName, _Color, algorithmParam);
		getPreStepParam(start, algorithmParam);
		algorithmParam.SMUData = GetSMUResult(start);
		return algorithmParam;
	}
}
