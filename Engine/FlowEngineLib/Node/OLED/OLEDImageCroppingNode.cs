using System.IO;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.OLED;

public class OLEDImageCroppingNode : CVBaseServerNodeIn2Hub
{
	private string _TempName;

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
			m_ctrl_temp.Value = value;
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

	public OLEDImageCroppingNode()
		: base("图像裁剪2", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		base.Height = 90;
		operatorCode = "OLED.GetRIAand";
		m_in_text = "IN_IMG";
		m_in2_text = "IN_ROI";
		_TempName = "";
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "模板:", _TempName);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmPreStepParam algorithmPreStepParam = new AlgorithmPreStepParam();
		OLEDImageCroppingParam oLEDImageCroppingParam = new OLEDImageCroppingParam(_TempName, _ImgFileName);
		if (!string.IsNullOrEmpty(_ImgFileName) && File.Exists(_ImgFileName))
		{
			string text = Path.GetExtension(_ImgFileName).ToLower();
			if (text.Contains("tif"))
			{
				oLEDImageCroppingParam.FileType = FileExtType.Tif;
			}
			else if (text.Contains("cvraw"))
			{
				oLEDImageCroppingParam.FileType = FileExtType.Raw;
			}
			else if (text.Contains("cvcie"))
			{
				oLEDImageCroppingParam.FileType = FileExtType.CIE;
			}
			else
			{
				oLEDImageCroppingParam.FileType = FileExtType.Tif;
			}
		}
		else
		{
			oLEDImageCroppingParam.FileType = FileExtType.None;
		}
		getPreStepParam(0, oLEDImageCroppingParam);
		getPreStepParam(1, algorithmPreStepParam);
		oLEDImageCroppingParam.ROI_MasterId = algorithmPreStepParam.MasterId;
		return oLEDImageCroppingParam;
	}
}
