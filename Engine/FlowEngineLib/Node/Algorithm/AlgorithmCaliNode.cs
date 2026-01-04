using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

[STNode("/03_3 校正")]
public class AlgorithmCaliNode : CVBaseServerNode
{
	private string _TempName;

	private int _TempId;

	private string _ImgFileName;

	private STNodeEditText<string> m_ctrl_temp;

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
		m_ctrl_temp.Value = _TempName;
	}

	public AlgorithmCaliNode()
		: base("色差校正", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "CaliAngleShift";
		_TempName = "";
		_TempId = -1;
		base.MaxTime = 30000;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "模板:", _TempName);
	}

	private void setAlgorithmType()
	{
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmCaliParam algorithmCaliParam = new AlgorithmCaliParam(_TempName);
		if (!string.IsNullOrEmpty(_ImgFileName))
		{
			algorithmCaliParam.ImgFileName = _ImgFileName;
			algorithmCaliParam.FileType = GetImageFileType(_ImgFileName);
		}
		else
		{
			getPreStepParam(start, algorithmCaliParam);
		}
		return algorithmCaliParam;
	}
}
