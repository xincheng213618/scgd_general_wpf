#nullable disable
using System.ComponentModel;
using ColorVision.Engine.PropertyEditor;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

[STNode("/03_4 KB")]
[STNodeSerializationModel("FlowEngineLib.dll|FlowEngineLib.Node.Algorithm.AlgorithmKBOutputNode")]
public class AlgorithmKBOutputNode : CVBaseServerNode
{
	[STNodeProperty("参数模板", "参数模板", true)]
	[PropertyEditorType(typeof(KbTemplatePropertiesEditor))]
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
			OnPropertyChanged();
		}
	}

	public AlgorithmKBOutputNode()
		: base("KB输出", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "KB.Output";
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		CreateTempControl(m_custom_item);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		KBOutputParam kBOutputParam = new KBOutputParam();
		BuildTemp(kBOutputParam);
		getPreStepParam(start, kBOutputParam);
		return kBOutputParam;
	}
}
