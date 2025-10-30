using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

[STNode("/03_1 关注点")]
public class POINode : CVBaseServerNode
{
	private string _TemplateName;

	private int _TempId;

	private string _FilterTemplateName;

	private string _ReviseTemplateName;

	private string _OutputTemplateName;

	private string _ImgFileName;

	private bool _IsCCTWave;

	private bool _IsSubPixel;

	private STNodeEditText<string> m_ctrl_temp;

	private STNodeEditText<string> m_ctrl_filtertemp;

	private STNodeEditText<string> m_ctrl_outtemp;

	[STNodeProperty("参数模板", "参数模板", true)]
	public string TemplateName
	{
		get
		{
			return _TemplateName;
		}
		set
		{
			_TemplateName = value;
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

	[STNodeProperty("过滤模板", "过滤模板", true)]
	public string FilterTemplateName
	{
		get
		{
			return _FilterTemplateName;
		}
		set
		{
			_FilterTemplateName = value;
			setFilterReviseTemp();
		}
	}

	[STNodeProperty("修正模板", "修正模板", true)]
	public string ReviseTemplateName
	{
		get
		{
			return _ReviseTemplateName;
		}
		set
		{
			_ReviseTemplateName = value;
			setFilterReviseTemp();
		}
	}

	[STNodeProperty("文件输出模板", "文件输出模板", true)]
	public string OutputTemplateName
	{
		get
		{
			return _OutputTemplateName;
		}
		set
		{
			_OutputTemplateName = value;
			m_ctrl_outtemp.Value = value;
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

	[STNodeProperty("色温/波长", "色温/波长", true)]
	public bool IsCCTWave
	{
		get
		{
			return _IsCCTWave;
		}
		set
		{
			_IsCCTWave = value;
		}
	}

	[STNodeProperty("亚像素", "亚像素", true)]
	public bool IsSubPixel
	{
		get
		{
			return _IsSubPixel;
		}
		set
		{
			_IsSubPixel = value;
		}
	}

	public POINode()
		: base("关注点算法", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "POI";
		_TemplateName = "";
		_TempId = -1;
		_FilterTemplateName = "";
		_ReviseTemplateName = "";
		_OutputTemplateName = "";
		_ImgFileName = "";
		_IsSubPixel = false;
		_IsCCTWave = true;
		base.Height += 50;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		base.InputOptions.Add(STNodeOption.Empty);
		base.InputOptions.Add(STNodeOption.Empty);
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "模板:", $"{_TempId}:{_TemplateName}");
		m_custom_item.Y += 25;
		m_ctrl_filtertemp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "F/R:", getFilterReviseTemp());
		m_custom_item.Y += 25;
		m_ctrl_outtemp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "输出:", _OutputTemplateName);
	}

	private void setFilterReviseTemp()
	{
		m_ctrl_filtertemp.Value = getFilterReviseTemp();
	}

	private string getFilterReviseTemp()
	{
		return $"{_FilterTemplateName}/{_ReviseTemplateName}";
	}

	private void setTempName()
	{
		m_ctrl_temp.Value = $"{_TempId}:{_TemplateName}";
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmPreStepParam param = new AlgorithmPreStepParam();
		getPreStepParam(start, param);
		return new POIDataParam(_ImgFileName, _TempId, _TemplateName, _FilterTemplateName, _ReviseTemplateName, _OutputTemplateName, param, _IsSubPixel, _IsCCTWave)
		{
			SMUData = GetSMUResult(start)
		};
	}
}
