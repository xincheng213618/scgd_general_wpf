using System.Collections.Generic;
using FlowEngineLib.Base;
using FlowEngineLib.Control;
using FlowEngineLib.Node.Spectrum;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Spectum;

public class SpectrumLoopNode : CVBaseLoopServerNode<SpectrumNodeProperty>
{
	[STNodeProperty("参数", "Get or set the Spectrum params", false, DescriptorType = typeof(LoopNodePropertyDescriptor<SpectrumNodeProperty, FormSpectumParam>))]
	public List<SpectrumNodeProperty> Params
	{
		get
		{
			return _params;
		}
		set
		{
			_params = value;
			updateUI();
		}
	}

	public SpectrumLoopNode()
		: base("光谱仪.For", "Spectrum", "SVR.Spectrum.Default", "DEV.Spectrum.Default")
	{
		operatorCode = "GetData";
	}

	protected override object getBaseEventData(CVStartCFC start, SpectrumNodeProperty property)
	{
		SpectrumParamData result = null;
		switch (property.Cmd)
		{
		case SPCommCmdType.检测:
			operatorCode = "GetData";
			result = property.Data;
			break;
		case SPCommCmdType.校零:
			operatorCode = "InitDark";
			break;
		}
		return result;
	}
}
