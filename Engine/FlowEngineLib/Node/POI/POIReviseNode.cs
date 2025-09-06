using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using log4net;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.POI;

[STNode("/03_1 关注点")]
public class POIReviseNode : CVBaseServerNodeIn2Hub
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(POIReviseNode));

	private string _TemplateName;

	protected string _Output;

	private string _POIPointName;

	private bool _IsSelfResultRevise;

	private STNodeEditText<string> m_ctrl_temp;

	private STNodeEditText<string> m_ctrl_out_temp;

	private STNodeEditText<string> m_ctrl_poi;

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
			m_ctrl_temp.Value = value;
		}
	}

	[STNodeProperty("修正输出", "修正输出", true)]
	public string Output
	{
		get
		{
			return _Output;
		}
		set
		{
			_Output = value;
			m_ctrl_out_temp.Value = value;
		}
	}

	[STNodeProperty("POI点名称", "POI点名称", true)]
	public string POIPointName
	{
		get
		{
			return _POIPointName;
		}
		set
		{
			_POIPointName = value;
			m_ctrl_poi.Value = value;
		}
	}

	[STNodeProperty("POI修正", "POI修正", true)]
	public bool IsSelfResultRevise
	{
		get
		{
			return _IsSelfResultRevise;
		}
		set
		{
			_IsSelfResultRevise = value;
		}
	}

	public POIReviseNode()
		: base("关注点修正标定", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		base.Height += 50;
		operatorCode = "POIReviseGen";
		m_in_text = "IN_POI";
		m_in2_text = "IN_SPE";
		_TemplateName = "";
		_Output = "";
		_POIPointName = "";
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "参数:", _TemplateName);
		m_custom_item.Y += 25;
		m_ctrl_out_temp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "输出:", _Output);
		m_custom_item.Y += 25;
		m_ctrl_poi = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "POI:", _POIPointName);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmPreStepParam[] array = new AlgorithmPreStepParam[masterInput.Length];
		for (int i = 0; i < masterInput.Length; i++)
		{
			AlgorithmPreStepParam algorithmPreStepParam = new AlgorithmPreStepParam();
			getPreStepParam(i, algorithmPreStepParam);
			array[i] = algorithmPreStepParam;
		}
		if (logger.IsDebugEnabled)
		{
			logger.DebugFormat("PreStepParams => {0}", JsonConvert.SerializeObject(array));
		}
		return new POIReviseData(array[1].MasterId, array[0].MasterId, _TemplateName, _Output, _POIPointName, _IsSelfResultRevise);
	}
}
