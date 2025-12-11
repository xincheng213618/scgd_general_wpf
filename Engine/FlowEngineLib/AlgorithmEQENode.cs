using System.Drawing;
using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

[STNode("/03_2 Algorithm")]
public class AlgorithmEQENode : CVBaseServerNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(AlgorithmEQENode));

	private string _TempName;

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

	public AlgorithmEQENode()
		: base("CalcEQE", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		m_is_out_release = false;
		m_has_svr_item = false;
		_TempName = "";
		operatorCode = "CalcEQE";
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		Rectangle custom_item = m_custom_item;
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<string>), custom_item, "模板:", _TempName);
	}

	private void setTempName()
	{
		m_ctrl_temp.Value = _TempName;
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		EQEParam eQEParam = new EQEParam(_TempName);
		getPreStepParam(start, eQEParam);
		return eQEParam;
	}
}
