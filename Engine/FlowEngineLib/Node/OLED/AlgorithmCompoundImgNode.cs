using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.OLED;

[STNode("/03_2 Algorithm")]
public class AlgorithmCompoundImgNode : CVBaseServerNodeIn2Hub
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(AlgorithmCompoundImgNode));

	private int _OrderIndex;

	private string _TempName;

	private int _TempId;

	private string _OutputFileName;

	private int _BufferLen;

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

	[STNodeProperty("输出文件", "输出文件", true)]
	public string OutputFileName
	{
		get
		{
			return _OutputFileName;
		}
		set
		{
			_OutputFileName = value;
		}
	}

	[STNodeProperty("缓存大小", "缓存大小", true)]
	public int BufferLen
	{
		get
		{
			return _BufferLen;
		}
		set
		{
			_BufferLen = value;
		}
	}

	private void setTempName()
	{
		m_ctrl_temp.Value = $"{_TempId}:{_TempName}";
	}

	public AlgorithmCompoundImgNode()
		: base("图像拼接", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "CompoundImg";
		_TempName = "";
		_TempId = -1;
		m_in_text = "IN_IMG1";
		m_in2_text = "IN_IMG2";
		_OutputFileName = "result.tif";
		_OrderIndex = -1;
		_BufferLen = 1024;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "模板:", $"{_TempId}:{_TempName}");
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmPreStepParam algorithmPreStepParam = new AlgorithmPreStepParam();
		AlgorithmPreStepParam algorithmPreStepParam2 = new AlgorithmPreStepParam();
		getPreStepParam(0, algorithmPreStepParam);
		getPreStepParam(1, algorithmPreStepParam2);
		return new AlgorithmCompoundImgParam(_TempName, algorithmPreStepParam, algorithmPreStepParam2, _OrderIndex, _BufferLen, _OutputFileName);
	}
}
