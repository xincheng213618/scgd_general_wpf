using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using FlowEngineLib.Node.OLED;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

[STNode("/03_2 Algorithm")]
public class AlgorithmFindLEDNode : CVBaseServerNode
{
	private CVOLED_COLOR _Color;

	private CVOLED_FDAType _FDAType;

	private PointFloat[] _FixedLEDPoint;

	private string _OutputFileName;

	private string _ImgPosResultFile;

	private STNodeEditText<CVOLED_COLOR> m_ctrl_color;

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
			setTempName(value);
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
			SetFDAType(value);
		}
	}

	[STNodeProperty("固定点", "固定点", true, DescriptorType = typeof(OLEDNodeDescriptor))]
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

	[STNodeProperty("定位输出", "定位输出", true)]
	public string ImgPosResultFile
	{
		get
		{
			return _ImgPosResultFile;
		}
		set
		{
			_ImgPosResultFile = value;
		}
	}

	public AlgorithmFindLEDNode()
		: base("LED定位", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "FindLED";
		_ImgPosResultFile = "ImgPos.tif";
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
		base.Height += 25;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		CreateTempControl(m_custom_item);
		m_custom_item.Y += 25;
		m_ctrl_color = CreateControl(typeof(STNodeEditText<CVOLED_COLOR>), m_custom_item, "颜色:", _Color);
	}

	private void SetFDAType(CVOLED_FDAType newValue)
	{
		_FDAType = newValue;
		switch (_FDAType)
		{
		case CVOLED_FDAType.Mem:
		case CVOLED_FDAType.FixedLED:
			OutputFileName = "pos.csv";
			break;
		case CVOLED_FDAType.OutFile:
		case CVOLED_FDAType.FixedLEDToFile:
			OutputFileName = "pos.dat";
			break;
		}
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmOLEDParam algorithmOLEDParam = new AlgorithmOLEDParam(_OutputFileName, _FDAType, _ImgPosResultFile, _FixedLEDPoint);
		BuildImageParam(_Color, algorithmOLEDParam);
		getPreStepParam(start, algorithmOLEDParam);
		algorithmOLEDParam.SMUData = GetSMUResult(start);
		return algorithmOLEDParam;
	}
}
