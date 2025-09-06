using System.Drawing;
using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.POI;

public class POICADMappingNode : CVBaseServerNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(POICADMappingNode));

	protected string _GlobalVariableName;

	private string _TemplateName;

	private POIBuildType _MappingType;

	private string _CADFileName;

	private string _PrefixName;

	private STNodeEditText<POIBuildType> m_ctrl_type;

	private STNodeEditText<string> m_ctrl_temp;

	private STNodeEditText<string> m_ctrl_prefix;

	[STNodeProperty("全局变量", "全局变量", true)]
	public string GlobalVariableName
	{
		get
		{
			return _GlobalVariableName;
		}
		set
		{
			_GlobalVariableName = value;
		}
	}

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
		m_ctrl_type = new STNodeEditText<POIBuildType>();
		m_ctrl_type.Text = "类型:";
		m_ctrl_type.DisplayRectangle = m_custom_item;
		m_ctrl_type.Value = _MappingType;
		base.Controls.Add(m_ctrl_type);
		m_ctrl_temp = new STNodeEditText<string>();
		m_ctrl_temp.Text = "模板:";
		m_ctrl_temp.DisplayRectangle = new Rectangle(m_custom_item.X, m_custom_item.Y + 25, m_custom_item.Width, m_custom_item.Height);
		m_ctrl_temp.Value = _TemplateName;
		base.Controls.Add(m_ctrl_temp);
		m_ctrl_prefix = new STNodeEditText<string>();
		m_ctrl_prefix.Text = "前缀:";
		m_ctrl_prefix.DisplayRectangle = new Rectangle(m_custom_item.X, m_custom_item.Y + 50, m_custom_item.Width, m_custom_item.Height);
		m_ctrl_prefix.Value = _PrefixName;
		base.Controls.Add(m_ctrl_prefix);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		POICADMappingData pOICADMappingData = new POICADMappingData(_TemplateName, _PrefixName, _CADFileName, _MappingType);
		getPreStepParam(start, pOICADMappingData);
		return pOICADMappingData;
	}
}
