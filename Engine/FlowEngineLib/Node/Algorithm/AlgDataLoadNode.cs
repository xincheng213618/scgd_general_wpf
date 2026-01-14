using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

public class AlgDataLoadNode : CVBaseServerNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(AlgDataLoadNode));

	[STNodeProperty("模板", "模板", true)]
	public string TempName
	{
		get
		{
			return _TempName;
		}
		set
		{
			_TempName = value;
			m_ctrl_temp.Value = value;
		}
	}

	public AlgDataLoadNode()
		: base("数据加载", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "DataLoad";
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "模板:", base.TempDisName);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		return new DataLoadData
		{
			TemplateParam = BuildTemp()
		};
	}
}
