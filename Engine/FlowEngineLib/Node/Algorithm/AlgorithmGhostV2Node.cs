using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

[STNode("Algorithm", CategoryOrder = 320)]
public class AlgorithmGhostV2Node : CVBaseServerNodeHub
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
			setTempName(value);
			OnPropertyChanged();
		}
	}

	[STNodeProperty("图像文件", "图像文件", true)]
	[System.ComponentModel.DataAnnotations.Display(Order = -100)]
	[System.ComponentModel.PropertyEditorTypeAttribute(typeof(System.ComponentModel.TextSelectFilePropertiesEditor))]
	public string ImgFileName
	{
		get
		{
			return _ImgFileName;
		}
		set
		{
			_ImgFileName = value;
			OnPropertyChanged();
		}
	}

	public AlgorithmGhostV2Node()
		: base("ARVR.Ghost算法", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "Ghost";
		m_in_text = "IN_IMG";
		m_in_textHub[0] = "IN_IMG";
		m_in_textHub[1] = "IN_CIE";
		base.MaxTime = 15000;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		CreateTempControl(m_custom_item);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmGhostInputParam algorithmGhostInputParam = new AlgorithmGhostInputParam(bufferLen: 1024);
		getPreStepParam(0, algorithmGhostInputParam);
		AlgorithmPreStepParam algorithmPreStepParam = new AlgorithmPreStepParam();
		getPreStepParam(1, algorithmPreStepParam);
		algorithmGhostInputParam.CIE_MasterId = algorithmPreStepParam.MasterId;
		BuildImageParam(algorithmGhostInputParam);
		algorithmGhostInputParam.SMUData = GetSMUResult(start);
		return algorithmGhostInputParam;
	}
}
