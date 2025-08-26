using System;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.POI;

[STNode("/03_1 关注点")]
public class RealPOINode : CVBaseServerNodeIn2Hub
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(RealPOINode));

	private string _FilterTemplateName;

	private string _ReviseTemplateName;

	private string _ReviseFileName;

	private string _OutputTemplateName;

	private bool _IsSubPixel;

	private POIPointTypes _POIType;

	private float _POIHeight;

	private float _POIWidth;

	private bool _IsResultAdd;

	private bool _IsCCTWave;

	private STNodeEditText<string> m_ctrl_temp;

	private STNodeEditText<string> m_ctrl_type;

	private STNodeEditText<string> m_ctrl_outtemp;

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

	[STNodeProperty("二次修正文件", "二次修正文件", true)]
	public string ReviseFileName
	{
		get
		{
			return _ReviseFileName;
		}
		set
		{
			_ReviseFileName = value;
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

	[STNodeProperty("POI类型", "POI类型", true)]
	public POIPointTypes POIType
	{
		get
		{
			return _POIType;
		}
		set
		{
			setPOIType(value);
		}
	}

	[STNodeProperty("POI高度", "POI高度", true)]
	public float POIHeight
	{
		get
		{
			return _POIHeight;
		}
		set
		{
			setHeight(value, syncWid: true);
		}
	}

	[STNodeProperty("POI宽度", "POI宽度", true)]
	public float POIWidth
	{
		get
		{
			return _POIWidth;
		}
		set
		{
			setWidth(value, syncHei: true);
		}
	}

	[STNodeProperty("POI新增", "POI新增", true)]
	public bool IsResultAdd
	{
		get
		{
			return _IsResultAdd;
		}
		set
		{
			_IsResultAdd = value;
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

	public RealPOINode()
		: base("实时关注点算法", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default", 2)
	{
		base.Height += 50;
		operatorCode = "Real_POI";
		m_in_text = "IN_CIE";
		m_in2_text = "IN_POI";
		_FilterTemplateName = "";
		_ReviseTemplateName = "";
		_OutputTemplateName = "";
		_ReviseFileName = "";
		_POIType = POIPointTypes.None;
		_POIWidth = 10f;
		_POIHeight = 10f;
		_IsResultAdd = false;
		_IsSubPixel = false;
		_IsCCTWave = true;
        logger.Info($"RealPOINode");
    }

    protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_type = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "类型:", getTypeSize());
		m_custom_item.Y += 25;
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "F/R:", getFilterReviseTemp());
		m_custom_item.Y += 25;
		m_ctrl_outtemp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "输出:", _OutputTemplateName);
	}

	private void setHeight(float value, bool syncWid)
	{
		if (_IsSubPixel)
		{
			_POIHeight = value;
		}
		else
		{
			setHeightInt(Convert.ToInt32(value));
		}
		setTypeSize();
		if (syncWid && _POIType == POIPointTypes.Circle)
		{
			setWidth(_POIHeight, syncHei: false);
		}
	}

	private void setHeightInt(int value)
	{
		if (value % 2 == 0)
		{
			_POIHeight = value;
		}
		else
		{
			_POIHeight = value + 1;
		}
	}

	private void setWidth(float value, bool syncHei)
	{
		if (_IsSubPixel)
		{
			_POIWidth = value;
		}
		else
		{
			setWidthInt(Convert.ToInt32(value));
		}
		setTypeSize();
		if (syncHei && _POIType == POIPointTypes.Circle)
		{
			setHeight(_POIWidth, syncWid: false);
		}
	}

	private void setWidthInt(int value)
	{
		if (value % 2 == 0)
		{
			_POIWidth = value;
		}
		else
		{
			_POIWidth = value + 1;
		}
	}

	private void setFilterReviseTemp()
	{
		m_ctrl_temp.Value = getFilterReviseTemp();
	}

	private string getFilterReviseTemp()
	{
		return $"{_FilterTemplateName}/{_ReviseTemplateName}";
	}

	private void setPOIType(POIPointTypes value)
	{
		_POIType = value;
		setTypeSize();
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		POITypeData pOITypeData = default(POITypeData);
		pOITypeData.PointType = _POIType;
		pOITypeData.Width = _POIWidth;
		pOITypeData.Height = _POIHeight;
		POITypeData poiData = pOITypeData;
		AlgorithmPreStepParam[] array = new AlgorithmPreStepParam[masterInput.Length];
		for (int i = 0; i < masterInput.Length; i++)
		{
			AlgorithmPreStepParam algorithmPreStepParam = new AlgorithmPreStepParam();
			getPreStepParam(masterInput[i], algorithmPreStepParam);
			array[i] = algorithmPreStepParam;
		}
		if(logger.IsDebugEnabled)
			logger.Debug($"RealPOINode Input1 MasterId: {array[0].MasterId}, Input2 MasterId: {array[1].MasterId}");

        RealPOIData result = new RealPOIData(_FilterTemplateName, _ReviseTemplateName, _ReviseFileName, _OutputTemplateName, poiData, array[0].MasterId, array[1].MasterId, _IsResultAdd, _IsSubPixel, _IsCCTWave);
		if (start.Data.ContainsKey("Image"))
		{
			start.Data.Remove("Image");
		}
		return result;
	}

	private void setTypeSize()
	{
		m_ctrl_type.Value = getTypeSize();
	}

	private string getTypeSize()
	{
		string result = string.Empty;
		switch (_POIType)
		{
		case POIPointTypes.None:
		case POIPointTypes.SolidPoint_KB:
		case POIPointTypes.SolidPoint:
			result = _POIType.ToString();
			break;
		case POIPointTypes.Circle:
		case POIPointTypes.Rect:
			result = $"{_POIType.ToString()}[{_POIWidth}x{_POIHeight}]";
			break;
		}
		return result;
	}
}
