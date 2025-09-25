using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Algorithm;

[STNode("/03_2 Algorithm")]
public class AlgorithmNode : CVBaseServerNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(AlgorithmNode));

	private int _OrderIndex;

	private AlgorithmType _Algorithm;

	private string _TempName;

	private int _TempId;

	private string _POITempName;

	private int _POITempId;

	private string _ImgFileName;

	private CVOLED_COLOR _Color;

	private bool _IsInversion;

	private int _BufferLen;

	private STNodeEditText<string> m_ctrl_temp;

	private STNodeEditText<AlgorithmType> m_ctrl_editText;

	private STNodeEditText<CVOLED_COLOR> m_ctrl_color;

	[STNodeProperty("o-index", "Input Order Index", true, false, false)]
	public int OrderIndex
	{
		get
		{
			return _OrderIndex;
		}
		set
		{
			_OrderIndex = value;
		}
	}

	[STNodeProperty("算子类别", "算子类别", true)]
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
			_TempName = value;
			setTempName();
		}
	}

	[STNodeProperty("参数模板ID", "参数模板ID", true)]
	public int TempId
	{
		get
		{
			return _TempId;
		}
		set
		{
			_TempId = value;
			setTempName();
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

	[STNodeProperty("POI模板ID", "POI模板ID", true)]
	public int POITempId
	{
		get
		{
			return _POITempId;
		}
		set
		{
			_POITempId = value;
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

	[STNodeProperty("图像水平翻转", "图像水平翻转", true)]
	public bool IsInversion
	{
		get
		{
			return _IsInversion;
		}
		set
		{
			_IsInversion = value;
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

	private void setTempName()
	{
		m_ctrl_temp.Value = $"{_TempId}:{_TempName}";
	}

	public AlgorithmNode()
		: base("AI算法", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "MTF";
		_TempName = "";
		base.Height += 50;
		_OrderIndex = -1;
		_TempId = -1;
		_POITempId = -1;
		_BufferLen = 1024;
		_Color = CVOLED_COLOR.GREEN;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_editText = CreateControl(typeof(STNodeEditText<AlgorithmType>), m_custom_item, "算法:", _Algorithm);
		m_custom_item.Y += 25;
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "模板:", $"{_TempId}:{_TempName}");
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
		algorithmParam.Color = _Color;
		algorithmParam.ImgFileName = _ImgFileName;
		algorithmParam.FileType = GetImageFileType(_ImgFileName);
		algorithmParam.IsInversion = _IsInversion;
		algorithmParam.BufferLen = _BufferLen;
		algorithmParam.TemplateParam = new CVTemplateParam
		{
			ID = _TempId,
			Name = _TempName
		};
		getPreStepParam(start, algorithmParam);
		algorithmParam.SMUData = GetSMUResult(start);
		return algorithmParam;
	}
}
