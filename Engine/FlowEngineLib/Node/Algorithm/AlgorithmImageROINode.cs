using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

[STNode("/03_3 Image")]
public class AlgorithmImageROINode : CVBaseServerNode
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

	public AlgorithmImageROINode()
		: base("图像裁剪", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "Image.ROI";
		_OutputFileName = "imgROI.cvraw";
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		CreateTempControl(m_custom_item);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmImageROIParam algorithmImageROIParam = new AlgorithmImageROIParam(_OutputFileName);
		BuildImageParam(algorithmImageROIParam);
		getPreStepParam(start, algorithmImageROIParam);
		return algorithmImageROIParam;
	}
}
