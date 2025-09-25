using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.POI;

[STNode("/03_1 关注点")]
public class BuildPOI2Node : CVBaseServerNodeIn2Hub
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(BuildPOI2Node));

	private string _TemplateName;

	private int _TempId;

	private POIBuildType _BuildType;

	private string _PrefixName;

	private POIPointTypes _POIType;

	private int _POIHeight;

	private int _POIWidth;

	private string _ImgFileName;

	private POIStorageModel _POIOutput;

	private string _OutputFileName;

	private string _SavePOITempName;

	private int _BufferLen;

	private STNodeEditText<POIBuildType> m_ctrl_type;

	private STNodeEditText<string> m_ctrl_temp;

	private STNodeEditText<string> m_ctrl_poi_type;

	private STNodeEditText<string> m_ctrl_out;

	[STNodeProperty("模板名称", "模板名称", true)]
	public string TemplateName
	{
		get
		{
			return _TemplateName;
		}
		set
		{
			_TemplateName = value;
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

	[STNodeProperty("布点类型", "布点类型", true)]
	public POIBuildType BuildType
	{
		get
		{
			return _BuildType;
		}
		set
		{
			_BuildType = value;
			setBuildType();
		}
	}

	[STNodeProperty("名称前缀", "名称前缀", true)]
	public string PrefixName
	{
		get
		{
			return _PrefixName;
		}
		set
		{
			_PrefixName = value;
			setOutputPre();
		}
	}

	[STNodeProperty("POI类型", "POI类型", true)]
	public POIPointTypes POIType
	{
		get
		{
			return _POIType;
		}
		set
		{
			setPOIType(value);
		}
	}

	[STNodeProperty("POI高度", "POI高度", true)]
	public int POIHeight
	{
		get
		{
			return _POIHeight;
		}
		set
		{
			setHeight(value, syncWid: true);
		}
	}

	[STNodeProperty("POI宽度", "POI宽度", true)]
	public int POIWidth
	{
		get
		{
			return _POIWidth;
		}
		set
		{
			setWidth(value, syncHei: true);
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

	[STNodeProperty("输出类别", "输出类别", true)]
	public POIStorageModel POIOutput
	{
		get
		{
			return _POIOutput;
		}
		set
		{
			_POIOutput = value;
			setOutputPre();
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

	[STNodeProperty("输出POI模板", "输出POI模板", true)]
	public string SavePOITempName
	{
		get
		{
			return _SavePOITempName;
		}
		set
		{
			_SavePOITempName = value;
		}
	}

	private void setTempName()
	{
		m_ctrl_temp.Value = $"{_TempId}:{_TemplateName}";
	}

	private void setPOIType(POIPointTypes value)
	{
		_POIType = value;
		setTypeSize();
	}

	public BuildPOI2Node()
		: base("关注点布点2", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "BuildPOI";
		m_in_text = "IN_IMG";
		m_in2_text = "IN_CAD";
		_OutputFileName = "pos.csv";
		_BuildType = POIBuildType.CADMapping;
		_POIType = POIPointTypes.None;
		_TemplateName = string.Empty;
		_TempId = -1;
		_PrefixName = string.Empty;
		_SavePOITempName = string.Empty;
		_BufferLen = 1024;
		base.Height = 175;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_type = CreateControl(typeof(STNodeEditText<POIBuildType>), m_custom_item, "类型:", _BuildType);
		m_custom_item.Y += 25;
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "模板:", $"{_TempId}:{_TemplateName}");
		m_custom_item.Y += 25;
		m_ctrl_poi_type = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "点类型:", getTypeSize());
		m_custom_item.Y += 25;
		m_ctrl_out = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "P/O:", getOutputPre());
	}

	private void setBuildType()
	{
		m_ctrl_type.Value = _BuildType;
	}

	private void setOutputPre()
	{
		m_ctrl_out.Value = getOutputPre();
	}

	private string getOutputPre()
	{
		return $"{_PrefixName}/{_POIOutput.ToString()}";
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		object obj = null;
		return _BuildType switch
		{
			POIBuildType.Common => buildParam(start), 
			POIBuildType.CADMapping => buildCADMappingParam(start), 
			_ => buildCADMappingParam(start), 
		};
	}

	private object buildCADMappingParam(CVStartCFC start)
	{
		AlgorithmPreStepParam algorithmPreStepParam = new AlgorithmPreStepParam();
		getPreStepParam(1, algorithmPreStepParam);
		POITypeData poiData = new POITypeData
		{
			PointType = _POIType,
			Width = _POIWidth,
			Height = _POIHeight
		};
		BuildPOIData buildPOIData = new BuildPOIData(_ImgFileName, new CADMappingParam(algorithmPreStepParam.MasterId), _TempId, _TemplateName, _POIOutput, _OutputFileName, _PrefixName, _BuildType, poiData, _SavePOITempName, string.Empty, _BufferLen);
		getPreStepParam(0, buildPOIData);
		return buildPOIData;
	}

	private object buildParam(CVStartCFC start)
	{
		POITypeData poiData = new POITypeData
		{
			PointType = _POIType,
			Width = _POIWidth,
			Height = _POIHeight
		};
		BuildPOIData buildPOIData = new BuildPOIData(_ImgFileName, _TempId, _TemplateName, _POIOutput, _OutputFileName, _PrefixName, _BuildType, poiData, _SavePOITempName, string.Empty, _BufferLen);
		getPreStepParam(start, buildPOIData);
		return buildPOIData;
	}

	private void setTypeSize()
	{
		m_ctrl_poi_type.Value = getTypeSize();
	}

	private string getTypeSize()
	{
		string result = string.Empty;
		switch (_POIType)
		{
		case POIPointTypes.None:
		case POIPointTypes.SolidPoint_KB:
		case POIPointTypes.SolidPoint:
			result = _POIType.ToString();
			break;
		case POIPointTypes.Circle:
		case POIPointTypes.Rect:
			result = $"{_POIType.ToString()}[{_POIWidth}x{_POIHeight}]";
			break;
		}
		return result;
	}

	private void setHeight(int value, bool syncWid)
	{
		if (value % 2 == 0)
		{
			_POIHeight = value;
		}
		else
		{
			_POIHeight = value + 1;
		}
		setTypeSize();
		if (syncWid && _POIType == POIPointTypes.Circle)
		{
			setWidth(_POIHeight, syncHei: false);
		}
	}

	private void setWidth(int value, bool syncHei)
	{
		if (value % 2 == 0)
		{
			_POIWidth = value;
		}
		else
		{
			_POIWidth = value + 1;
		}
		setTypeSize();
		if (syncHei && _POIType == POIPointTypes.Circle)
		{
			setHeight(_POIWidth, syncWid: false);
		}
	}
}
