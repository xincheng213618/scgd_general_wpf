using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

[STNode("/03_2 Algorithm")]
public class AlgorithmGhostV2Node : CVBaseServerNodeIn2Hub
{
	private int _BufferLen;

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

	[STNodeProperty("图像文件", "图像文件", true)]
	public string ImgFileName
	{
		get
		{
			return _ImgFileName;
		}
		set
		{
			_ImgFileName = value;
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

	public AlgorithmGhostV2Node()
		: base("ARVR.Ghost算法", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "Ghost";
		m_in_text = "IN_IMG";
		m_in2_text = "IN_CIE";
		_BufferLen = 1024;
		base.MaxTime = 15000;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		CreateTempControl(m_custom_item);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmGhostInputParam algorithmGhostInputParam = new AlgorithmGhostInputParam(_BufferLen);
		getPreStepParam(0, algorithmGhostInputParam);
		AlgorithmPreStepParam algorithmPreStepParam = new AlgorithmPreStepParam();
		getPreStepParam(1, algorithmPreStepParam);
		algorithmGhostInputParam.CIE_MasterId = algorithmPreStepParam.MasterId;
		BuildImageParam(algorithmGhostInputParam);
		algorithmGhostInputParam.SMUData = GetSMUResult(start);
		return algorithmGhostInputParam;
	}
}
