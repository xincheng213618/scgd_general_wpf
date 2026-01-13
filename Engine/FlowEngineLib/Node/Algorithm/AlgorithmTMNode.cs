using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

[STNode("/03_2 Algorithm")]
public class AlgorithmTMNode : CVBaseServerNode
{
	private string _TemplateFile;

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

	[STNodeProperty("模板文件", "模板文件", true)]
	public string TemplateFile
	{
		get
		{
			return _TemplateFile;
		}
		set
		{
			_TemplateFile = value;
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

	public AlgorithmTMNode()
		: base("模板匹配", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "MatchTemplate";
		_TempName = "";
		_TempId = -1;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		CreateTempControl(m_custom_item);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		TMParam tMParam = new TMParam(_TemplateFile);
		getPreStepParam(start, tMParam);
		BuildImageParam(tMParam);
		return tMParam;
	}
}
