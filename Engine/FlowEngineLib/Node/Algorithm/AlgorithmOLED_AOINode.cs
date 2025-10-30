using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using FlowEngineLib.Node.OLED;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

[STNode("/03_5 OLED")]
public class AlgorithmOLED_AOINode : CVBaseServerNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(OLEDRebuildPixelsNode));

	private AlgorithmOLED_AOIType _Algorithm;

	private CVOLED_COLOR _Color;

	private string _TempName;

	private int _TempId;

	private string _ImgFileName;

	private string _OutputFileName;

	private bool _VhLineEnable;

	private bool _PixelDefectEnable;

	private bool _MuraEnable;

	private STNodeEditText<string> m_ctrl_temp;

	private STNodeEditText<AlgorithmOLED_AOIType> m_ctrl_editText;

	[STNodeProperty("算子", "算子", true)]
	public AlgorithmOLED_AOIType Algorithm
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

	[STNodeProperty("输出文件", "输出文件", true)]
	public string OutputFileName
	{
		get
		{
			return _OutputFileName;
		}
		set
		{
			_OutputFileName = value;
		}
	}

	[STNodeProperty("线缺陷", "线缺陷启用/AOI总成", true)]
	public bool VhLineEnable
	{
		get
		{
			return _VhLineEnable;
		}
		set
		{
			_VhLineEnable = value;
		}
	}

	[STNodeProperty("点缺陷", "点缺陷启用/AOI总成", true)]
	public bool PixelDefectEnable
	{
		get
		{
			return _PixelDefectEnable;
		}
		set
		{
			_PixelDefectEnable = value;
		}
	}

	[STNodeProperty("Mura", "Mura启用/AOI总成", true)]
	public bool MuraEnable
	{
		get
		{
			return _MuraEnable;
		}
		set
		{
			_MuraEnable = value;
		}
	}

	private void setTempName()
	{
		m_ctrl_temp.Value = $"{_TempId}:{_TempName}";
	}

	public AlgorithmOLED_AOINode()
		: base("OLED.AOI", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		_Algorithm = AlgorithmOLED_AOIType.线缺陷;
		operatorCode = "OLED.FindVHLine";
		_TempName = "";
		_TempId = -1;
		_OutputFileName = "result.dat";
		_VhLineEnable = true;
		_PixelDefectEnable = true;
		_MuraEnable = true;
		base.Height += 25;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_editText = CreateControl(typeof(STNodeEditText<AlgorithmOLED_AOIType>), m_custom_item, "算子:", _Algorithm);
		m_custom_item.Y += 25;
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "模板:", $"{_TempId}:{_TempName}");
	}

	private void setAlgorithmType()
	{
		m_ctrl_editText.Value = _Algorithm;
		switch (_Algorithm)
		{
		case AlgorithmOLED_AOIType.线缺陷:
			operatorCode = "OLED.FindVHLine";
			break;
		case AlgorithmOLED_AOIType.点缺陷:
			operatorCode = "OLED.FindPixelDefectsForRebuildPicGrading";
			break;
		case AlgorithmOLED_AOIType.Mura:
			operatorCode = "OLED.FindMura";
			break;
		case AlgorithmOLED_AOIType.AOI总成:
			operatorCode = "OLED.ALL_AOI";
			break;
		}
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmOLEDAOIParam algorithmOLEDAOIParam = null;
		algorithmOLEDAOIParam = new AlgorithmOLEDAOIParam(_Color, _OutputFileName, _VhLineEnable, _MuraEnable, _PixelDefectEnable);
		algorithmOLEDAOIParam.ImgFileName = _ImgFileName;
		algorithmOLEDAOIParam.FileType = GetImageFileType(_ImgFileName);
		algorithmOLEDAOIParam.TemplateParam = new CVTemplateParam
		{
			ID = _TempId,
			Name = _TempName
		};
		getPreStepParam(start, algorithmOLEDAOIParam);
		algorithmOLEDAOIParam.SMUData = GetSMUResult(start);
		return algorithmOLEDAOIParam;
	}
}
