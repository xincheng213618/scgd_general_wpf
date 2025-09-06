using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using FlowEngineLib.Node.OLED;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

public class AlgorithmOLEDNode : CVBaseServerNode
{
	private AlgorithmOLEDType _Algorithm;

	private CVOLED_COLOR _Color;

	private string _TempName;

	private int _TempId;

	private CVOLED_FDAType _FDAType;

	private PointFloat[] _FixedLEDPoint;

	private string _ImgFileName;

	private string _OutputFileName;

	private STNodeEditText<string> m_ctrl_temp;

	private STNodeEditText<AlgorithmOLEDType> m_ctrl_editText;

	private STNodeEditText<CVOLED_COLOR> m_ctrl_color;

	[STNodeProperty("算子", "算子", true)]
	public AlgorithmOLEDType Algorithm
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

	[STNodeProperty("类别", "类别", true)]
	public CVOLED_FDAType FDAType
	{
		get
		{
			return _FDAType;
		}
		set
		{
			_FDAType = value;
		}
	}

	[STNodeProperty("固定点(FixedLED)", "固定点(FixedLED)", true, DescriptorType = typeof(OLEDNodeDescriptor))]
	public PointFloat[] FixedLEDPoint
	{
		get
		{
			return _FixedLEDPoint;
		}
		set
		{
			_FixedLEDPoint = value;
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

	private void setTempName()
	{
		m_ctrl_temp.Value = $"{_TempId}:{_TempName}";
	}

	public AlgorithmOLEDNode()
		: base("OLED算法", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "FindDotsArray";
		_TempName = "";
		_TempId = -1;
		_OutputFileName = "pos.csv";
		_FixedLEDPoint = new PointFloat[4]
		{
			new PointFloat
			{
				X = 0f,
				Y = 0f
			},
			new PointFloat
			{
				X = 0f,
				Y = 0f
			},
			new PointFloat
			{
				X = 0f,
				Y = 0f
			},
			new PointFloat
			{
				X = 0f,
				Y = 0f
			}
		};
		base.Height = 125;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_editText = CreateControl(typeof(STNodeEditText<AlgorithmOLEDType>), m_custom_item, "算法:", _Algorithm);
		m_custom_item.Y += 25;
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "模板:", $"{_TempId}:{_TempName}");
		m_custom_item.Y += 25;
		m_ctrl_color = CreateControl(typeof(STNodeEditText<CVOLED_COLOR>), m_custom_item, "颜色:", _Color);
	}

	private void setAlgorithmType()
	{
		m_ctrl_editText.Value = _Algorithm;
		_ = _Algorithm;
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmOLEDParam algorithmOLEDParam = null;
		algorithmOLEDParam = new AlgorithmOLEDParam(_Color, _OutputFileName);
		algorithmOLEDParam.ImgFileName = _ImgFileName;
		algorithmOLEDParam.FileType = GetImageFileType(_ImgFileName);
		algorithmOLEDParam.FixedLEDPoint = _FixedLEDPoint;
		algorithmOLEDParam.TemplateParam = new CVTemplateParam
		{
			ID = _TempId,
			Name = _TempName
		};
		getPreStepParam(start, algorithmOLEDParam);
		algorithmOLEDParam.FDAType = _FDAType;
		return algorithmOLEDParam;
	}
}
