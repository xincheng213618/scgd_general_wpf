using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

[STNode("/03_4 KB")]
public class AlgorithmKBNode : CVBaseServerNode
{
	private string _CaliTemplate;

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

	public AlgorithmKBNode()
		: base("KB算法", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "KB";
		_CaliTemplate = "";
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_temp = CreateTempControl(m_custom_item);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		KBParam kBParam = new KBParam(_CaliTemplate);
		getPreStepParam(start, kBParam);
		BuildImageParam(kBParam);
		return kBParam;
	}
}
