using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.POI;

[STNode("/03_1 关注点")]
public class POIAnalysisNode : CVBaseServerNode
{
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

	public POIAnalysisNode()
		: base("关注点分析", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "PoiAnalysis";
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "模板:", base.TempDisName);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		PoiAnalysisParam poiAnalysisParam = new PoiAnalysisParam(_TempId, _TempName);
		getPreStepParam(start, poiAnalysisParam);
		return poiAnalysisParam;
	}
}
