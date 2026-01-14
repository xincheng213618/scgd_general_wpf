using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.POI;

public class POICADMappingNode : CVBaseServerNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(POICADMappingNode));

	private string _TemplateName;

	private POIBuildType _MappingType;

	private string _CADFileName;

	private string _PrefixName;

	private STNodeEditText<POIBuildType> m_ctrl_type;

	private STNodeEditText<string> m_ctrl_prefix;

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
			m_ctrl_temp.Value = value;
		}
	}

	[STNodeProperty("CAD映射类型", "CAD映射类型", true)]
	public POIBuildType MappingType
	{
		get
		{
			return _MappingType;
		}
		set
		{
			_MappingType = value;
			m_ctrl_type.Value = value;
		}
	}

	[STNodeProperty("CAD点文件", "CAD点文件", true)]
	public string CADFileName
	{
		get
		{
			return _CADFileName;
		}
		set
		{
			_CADFileName = value;
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
			m_ctrl_prefix.Value = value;
		}
	}

	public POICADMappingNode()
		: base("关注点CAD布点1", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		base.Height = 125;
		operatorCode = "POI.CADMapping";
		_TemplateName = string.Empty;
		_PrefixName = string.Empty;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_type = CreateControl(typeof(STNodeEditText<POIBuildType>), m_custom_item, "类型:", _MappingType);
		m_custom_item.Y += 25;
		CreateTempControl(m_custom_item);
		m_custom_item.Y += 25;
		m_ctrl_prefix = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "前缀:", _PrefixName);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		POICADMappingData pOICADMappingData = new POICADMappingData(_PrefixName, _CADFileName, _MappingType);
		getPreStepParam(start, pOICADMappingData);
		pOICADMappingData.TemplateParam = BuildTemp();
		return pOICADMappingData;
	}
}
