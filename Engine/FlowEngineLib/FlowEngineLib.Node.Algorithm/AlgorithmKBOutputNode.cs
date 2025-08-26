using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

[STNode("/03_4 KB")]
public class AlgorithmKBOutputNode : CVBaseServerNode
{
	private string _TempName;

	private int _TempId;

	private STNodeEditText<string> m_ctrl_temp;

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
		m_ctrl_temp.Value = $"{_TempId}:{_TempName}";
	}

	public AlgorithmKBOutputNode()
		: base("KB输出", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "KB.Output";
		_TempName = "";
		_TempId = -1;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "模板:", $"{_TempId}:{_TempName}");
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		KBOutputParam kBOutputParam = new KBOutputParam(_TempId, _TempName);
		getPreStepParam(start, kBOutputParam);
		return kBOutputParam;
	}
}
