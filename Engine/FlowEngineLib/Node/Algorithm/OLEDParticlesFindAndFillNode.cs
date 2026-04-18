using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

[STNode("/03_5 OLED")]
public class OLEDParticlesFindAndFillNode : CVBaseServerNode
{
	private ParticlesMode _ParticlesType;

	private string _OutputFileName;

	private STNodeEditText<ParticlesMode> m_ctrl_type;

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

	[STNodeProperty("类别", "类别", true)]
	public ParticlesMode ParticlesType
	{
		get
		{
			return _ParticlesType;
		}
		set
		{
			SetParticlesType(value);
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

	public OLEDParticlesFindAndFillNode()
		: base("灰尘检测及修补", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "OLED.Particles.FindAndFill";
		_ParticlesType = ParticlesMode.Black;
		_OutputFileName = "result.tif";
		base.Height += 25;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_temp = CreateTempControl(m_custom_item);
		m_custom_item.Y += 25;
		m_ctrl_type = CreateControl(typeof(STNodeEditText<ParticlesMode>), m_custom_item, "类别:", _ParticlesType);
	}

	private void SetParticlesType(ParticlesMode value)
	{
		_ParticlesType = value;
		m_ctrl_type.Value = value;
		_OutputFileName = "result_" + value.ToString() + ".tif";
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		ParticlesFindAndFillParam particlesFindAndFillParam = new ParticlesFindAndFillParam(_ParticlesType, _OutputFileName);
		getPreStepParam(start, particlesFindAndFillParam);
		BuildImageParam(particlesFindAndFillParam);
		return particlesFindAndFillParam;
	}
}
