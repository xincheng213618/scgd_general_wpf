using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

[STNode("/03_2 Algorithm")]
public class AlgorithmEQENode : CVBaseServerNodeIn2Hub
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(AlgorithmEQENode));

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

	public AlgorithmEQENode()
		: base("CalcEQE", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		m_is_out_release = false;
		m_has_svr_item = false;
		m_in_text = "IN_SP";
		m_in2_text = "IN_SMU";
		operatorCode = "CalcEQE";
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		CreateTempControl(m_custom_item);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmPreStepParam algorithmPreStepParam = new AlgorithmPreStepParam();
		CalcEQEParam calcEQEParam = new CalcEQEParam();
		getPreStepParam(0, calcEQEParam);
		getPreStepParam(1, algorithmPreStepParam);
		BuildTemp(calcEQEParam);
		calcEQEParam.SMU_MasterId = algorithmPreStepParam.MasterId;
		return calcEQEParam;
	}
}
