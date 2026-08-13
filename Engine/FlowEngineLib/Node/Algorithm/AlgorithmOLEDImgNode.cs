using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

[STNode("/03_5 OLED")]
public class AlgorithmOLEDImgNode : CVBaseServerNode
{
	private AlgorithmOLEDImgType _Algorithm;
	private string _OutputFileName;
	private STNodeEditText<AlgorithmOLEDImgType> m_ctrl_editText;

	[STNodeProperty("算子", "算子", true)]
	public AlgorithmOLEDImgType Algorithm
	{
		get => _Algorithm;
		set
		{
			_Algorithm = value;
			SetAlgorithmType();
			OnPropertyChanged();
		}
	}

	[STNodeProperty("参数模板", "参数模板", true)]
	public string TempName
	{
		get => _TempName;
		set
		{
			_TempName = value;
			setTempName(value);
			OnPropertyChanged();
		}
	}

	[STNodeProperty("图像文件", "图像文件", true)]
	[System.ComponentModel.DataAnnotations.Display(Order = -100)]
	[System.ComponentModel.PropertyEditorTypeAttribute(typeof(System.ComponentModel.TextSelectFilePropertiesEditor))]
	public string ImgFileName
	{
		get => _ImgFileName;
		set
		{
			_ImgFileName = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("输出文件", "输出文件", true)]
	public string OutputFileName
	{
		get => _OutputFileName;
		set
		{
			_OutputFileName = value;
			OnPropertyChanged();
		}
	}

	public AlgorithmOLEDImgNode()
		: base("OLED.IMG", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		_Algorithm = AlgorithmOLEDImgType.局部图像增强;
		operatorCode = "OLED.LocalizationImageEnhan";
		_TempName = "";
		_TempId = -1;
		_OutputFileName = "result.cvraw";
		base.Height += 30;
		_MaxTime = 10000;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_editText = CreateControl(typeof(STNodeEditText<AlgorithmOLEDImgType>), m_custom_item, "算子:", _Algorithm);
		m_custom_item.Y += 25;
		CreateTempControl(m_custom_item);
	}

	private void SetAlgorithmType()
	{
		if (m_ctrl_editText != null)
		{
			m_ctrl_editText.Value = _Algorithm;
		}

		switch (_Algorithm)
		{
			case AlgorithmOLEDImgType.局部图像增强:
				operatorCode = "OLED.LocalizationImageEnhan";
				break;
			case AlgorithmOLEDImgType.解串扰:
				operatorCode = "OLED.Dediffusion";
				break;
		}
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmOLEDImgParam param = new(_OutputFileName);
		BuildImageParam(_ImgFileName, CVOLED_COLOR.GREEN, param);
		getPreStepParam(start, param);
		param.SMUData = GetSMUResult(start);
		return param;
	}
}
