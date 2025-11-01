using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.POI;

[STNode("/03_1 关注点")]
public class POIAnalysisNode : CVBaseServerNode
{
	private string _TemplateName;

	private int _TempId;

	private STNodeEditText<string> m_ctrl_temp;

	[STNodeProperty("参数模板", "参数模板", true)]
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

	private void setTempName()
	{
		m_ctrl_temp.Value = $"{_TempId}:{_TemplateName}";
	}

	public POIAnalysisNode()
		: base("关注点分析", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "PoiAnalysis";
		_TempId = -1;
		_TemplateName = "";
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "模板:", $"{_TempId}:{_TemplateName}");
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		PoiAnalysisParam poiAnalysisParam = new PoiAnalysisParam();
		getPreStepParam(start, poiAnalysisParam);
		poiAnalysisParam.TemplateParam = new CVTemplateParam
		{
			ID = _TempId,
			Name = _TemplateName
		};
		return poiAnalysisParam;
	}
}
