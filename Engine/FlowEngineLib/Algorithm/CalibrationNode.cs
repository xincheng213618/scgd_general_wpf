using System.IO;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Algorithm;

[STNode("/03_2 Algorithm")]
public class CalibrationNode : CVBaseServerNode
{
	protected string _GlobalVariableName;

	private int _OrderIndex;

	private string _TempName;

	private int _TempId;

	private string _ExpTempName;

	private string _ImgFileName;

	private STNodeEditText<string> m_ctrl_temp;

	private STNodeEditText<string> m_ctrl_temp_exp;

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
			setTempName();
		}
	}

	[STNodeProperty("参数模板ID", "参数模板ID", true)]
	public int TempId
	{
		get
		{
			return _TempId;
		}
		set
		{
			_TempId = value;
			setTempName();
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

	private void setTempName()
	{
		m_ctrl_temp.Value = $"{_TempId}:{_TempName}";
	}

	public CalibrationNode()
		: base("校正", "Calibration", "SVR.Calibration.Default", "DEV.Calibration.Default")
	{
		operatorCode = "Calibration";
		_TempName = "";
		_TempId = -1;
		_ExpTempName = "";
		base.Height += 25;
		_MaxTime = 10000;
		_OrderIndex = -1;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "校正:", $"{_TempId}:{_TempName}");
		m_custom_item.Y += 25;
		m_ctrl_temp_exp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "曝光:", _ExpTempName);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmPreStepParam param = new AlgorithmPreStepParam();
		getPreStepParam(start, param);
		CalibrationData calibrationData = new CalibrationData(_ImgFileName, _TempId, _TempName, _ExpTempName, param, _GlobalVariableName, _OrderIndex);
		if (!string.IsNullOrEmpty(_ImgFileName))
		{
			string text = Path.GetExtension(_ImgFileName).ToLower();
			if (text.Contains("tif"))
			{
				calibrationData.FileType = FileExtType.Tif;
			}
			else if (text.Contains("cvraw"))
			{
				calibrationData.FileType = FileExtType.Raw;
			}
			else if (text.Contains("cvcie"))
			{
				calibrationData.FileType = FileExtType.CIE;
			}
			else
			{
				calibrationData.FileType = FileExtType.Tif;
			}
		}
		return calibrationData;
	}
}
