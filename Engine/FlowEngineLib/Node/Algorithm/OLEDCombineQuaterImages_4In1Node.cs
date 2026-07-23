using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

[STNode("/03_5 OLED")]
public class OLEDCombineQuaterImages_4In1Node : CVBaseServerNodeHub
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(OLEDCombineQuaterImages_4In1Node));

	private CVOLED_COLOR _Color = CVOLED_COLOR.GREEN;

	private string _ImgFileName1;

	private string _ImgFileName2;

	private string _ImgFileName3;

	private string _ImgFileName4;

	private string _OutputFileName;

	[STNodeProperty("图像文件1", "图像文件1", true)]
	[System.ComponentModel.DataAnnotations.Display(Order = -100)]
	[System.ComponentModel.PropertyEditorTypeAttribute(typeof(System.ComponentModel.TextSelectFilePropertiesEditor))]
	public string ImgFileName1
	{
		get
		{
			return _ImgFileName1;
		}
		set
		{
			_ImgFileName1 = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("图像文件2", "图像文件2", true)]
	[System.ComponentModel.DataAnnotations.Display(Order = -100)]
	[System.ComponentModel.PropertyEditorTypeAttribute(typeof(System.ComponentModel.TextSelectFilePropertiesEditor))]
	public string ImgFileName2
	{
		get
		{
			return _ImgFileName2;
		}
		set
		{
			_ImgFileName2 = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("图像文件3", "图像文件3", true)]
	[System.ComponentModel.DataAnnotations.Display(Order = -100)]
	[System.ComponentModel.PropertyEditorTypeAttribute(typeof(System.ComponentModel.TextSelectFilePropertiesEditor))]
	public string ImgFileName3
	{
		get
		{
			return _ImgFileName3;
		}
		set
		{
			_ImgFileName3 = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("图像文件4", "图像文件4", true)]
	[System.ComponentModel.DataAnnotations.Display(Order = -100)]
	[System.ComponentModel.PropertyEditorTypeAttribute(typeof(System.ComponentModel.TextSelectFilePropertiesEditor))]
	public string ImgFileName4
	{
		get
		{
			return _ImgFileName4;
		}
		set
		{
			_ImgFileName4 = value;
			OnPropertyChanged();
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
			OnPropertyChanged();
		}
	}

	public OLEDCombineQuaterImages_4In1Node()
		: base("OLED图像4In1合并", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default", 4)
	{
		operatorCode = "OLED.CombineQuaterImages";
		_TempName = "";
		m_in_text = "IMG1";
		m_in_textHub[0] = "IMG1";
		m_in_textHub[1] = "IMG2";
		m_in_textHub[2] = "IMG3";
		m_in_textHub[3] = "IMG4";
		_OutputFileName = "result.cvcie";
		base.Height += 30;
		base.AutoSize = true;
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		OLEDCombineQuaterImagesParams oLEDCombineQuaterImagesParams = new OLEDCombineQuaterImagesParams(_Color, _ImgFileName1, _ImgFileName2, _ImgFileName3, _ImgFileName4, _OutputFileName);
		oLEDCombineQuaterImagesParams.TemplateParam = new CVTemplateParam
		{
			ID = -1,
			Name = _TempName
		};
		getPreStepParam(0, oLEDCombineQuaterImagesParams);
		oLEDCombineQuaterImagesParams.InputImages_MasterId[0] = oLEDCombineQuaterImagesParams.MasterId;
		AlgorithmPreStepParam algorithmPreStepParam = new AlgorithmPreStepParam();
		getPreStepParam(1, algorithmPreStepParam);
		oLEDCombineQuaterImagesParams.InputImages_MasterId[1] = algorithmPreStepParam.MasterId;
		AlgorithmPreStepParam algorithmPreStepParam2 = new AlgorithmPreStepParam();
		getPreStepParam(2, algorithmPreStepParam2);
		oLEDCombineQuaterImagesParams.InputImages_MasterId[2] = algorithmPreStepParam2.MasterId;
		AlgorithmPreStepParam algorithmPreStepParam3 = new AlgorithmPreStepParam();
		getPreStepParam(3, algorithmPreStepParam3);
		oLEDCombineQuaterImagesParams.InputImages_MasterId[3] = algorithmPreStepParam3.MasterId;
		return oLEDCombineQuaterImagesParams;
	}
}
