using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.OLED;

[STNode("/03_3 校正")]
public class Calibration2InNode : CVBaseServerNodeIn2Hub
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(Calibration2InNode));

	private string _ExpTempName;

	protected bool _IsSaveCIE;

	private STNodeEditText<string> m_ctrl_temp_exp;

	private STNodeEditText<bool> m_ctrl_saveCIE;

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

	[STNodeProperty("曝光模板", "曝光模板", true)]
	public string ExpTempName
	{
		get
		{
			return _ExpTempName;
		}
		set
		{
			_ExpTempName = value;
			m_ctrl_temp_exp.Value = value;
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

	[STNodeProperty("保存CIE文件", "保存CIE文件", true)]
	public bool IsSaveCIE
	{
		get
		{
			return _IsSaveCIE;
		}
		set
		{
			_IsSaveCIE = value;
			m_ctrl_saveCIE.Value = value;
		}
	}

	public Calibration2InNode()
		: base("校正2", "Calibration", "SVR.Calibration.Default", "DEV.Calibration.Default")
	{
		operatorCode = "Calibration";
		_TempName = "";
		_ExpTempName = "";
		_TempId = -1;
		_IsSaveCIE = true;
		m_in_text = "IN_IMG";
		m_in2_text = "IN_POI";
		base.Height += 50;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		CreateTempControl(m_custom_item, "校正模板:");
		m_custom_item.Y += 25;
		m_ctrl_temp_exp = CreateStringControl(m_custom_item, "曝光模板:", _ExpTempName);
		m_custom_item.Y += 25;
		m_ctrl_saveCIE = CreateControl(typeof(STNodeEditText<bool>), m_custom_item, "保存CIE文件:", _IsSaveCIE);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmPreStepParam param = new AlgorithmPreStepParam();
		AlgorithmPreStepParam algorithmPreStepParam = new AlgorithmPreStepParam();
		getPreStepParam(start, param);
		getPreStepParam(1, algorithmPreStepParam);
		CalibrationData calibrationData = new CalibrationData(_ExpTempName, param, _IsSaveCIE);
		BuildImageParam(calibrationData);
		calibrationData.POI_MasterId = algorithmPreStepParam.MasterId;
		return calibrationData;
	}
}
