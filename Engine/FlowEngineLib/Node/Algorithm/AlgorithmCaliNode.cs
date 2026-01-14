using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

[STNode("/03_3 校正")]
public class AlgorithmCaliNode : CVBaseServerNode
{
	private string _OutputFileName;

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

	public AlgorithmCaliNode()
		: base("色差校正", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "CaliAngleShift";
		_OutputFileName = "result.cvraw";
		base.MaxTime = 30000;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		CreateTempControl(m_custom_item);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmCaliParam algorithmCaliParam = new AlgorithmCaliParam(_OutputFileName);
		BuildImageParam(algorithmCaliParam);
		getPreStepParam(start, algorithmCaliParam);
		return algorithmCaliParam;
	}
}
