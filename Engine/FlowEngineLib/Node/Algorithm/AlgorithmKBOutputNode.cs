using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

[STNode("/03_4 KB")]
public class AlgorithmKBOutputNode : CVBaseServerNode
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
			_TempName = value;
			setTempName(value);
		}
	}

	public AlgorithmKBOutputNode()
		: base("KB输出", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "KB.Output";
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		CreateTempControl(m_custom_item);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		KBOutputParam kBOutputParam = new KBOutputParam();
		BuildTemp(kBOutputParam);
		getPreStepParam(start, kBOutputParam);
		return kBOutputParam;
	}
}
