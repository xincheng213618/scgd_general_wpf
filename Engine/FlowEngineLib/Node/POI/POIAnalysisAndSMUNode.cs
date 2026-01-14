using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.POI;

[STNode("/03_1 关注点")]
public class POIAnalysisAndSMUNode : CVBaseServerNodeIn2Hub
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

	public POIAnalysisAndSMUNode()
		: base("关注点分析_SMU", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "PoiAnalysis";
		m_in_text = "IN_POI";
		m_in2_text = "IN_SMU";
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		CreateTempControl(m_custom_item);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmPreStepParam algorithmPreStepParam = new AlgorithmPreStepParam();
		getPreStepParam(1, algorithmPreStepParam);
		PoiAnalysisAndSMUParam poiAnalysisAndSMUParam = new PoiAnalysisAndSMUParam(algorithmPreStepParam.MasterId);
		getPreStepParam(0, poiAnalysisAndSMUParam);
		BuildTemp(poiAnalysisAndSMUParam);
		return poiAnalysisAndSMUParam;
	}
}
