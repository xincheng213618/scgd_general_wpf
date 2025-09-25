using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Algorithm;

[STNode("/03_2 Algorithm")]
public class AlgorithmARVRNode : CVBaseServerNode
{
	private int _OrderIndex;

	private AlgorithmARVRType _Algorithm;

	private string _TempName;

	private int _TempId;

	private string _POITempName;

	private string _ImgFileName;

	private CVOLED_COLOR _Color;

	private int _BufferLen;

	private STNodeEditText<string> m_ctrl_temp;

	private STNodeEditText<AlgorithmARVRType> m_ctrl_editText;

	private STNodeEditText<CVOLED_COLOR> m_ctrl_color;

	[STNodeProperty("o-index", "Input Order Index", true, false, false)]
	public int OrderIndex
	{
		get
		{
			return _OrderIndex;
		}
		set
		{
			_OrderIndex = value;
		}
	}

	[STNodeProperty("算子类别", "算子类别", true)]
	public AlgorithmARVRType Algorithm
	{
		get
		{
			return _Algorithm;
		}
		set
		{
			_Algorithm = value;
			setAlgorithmType();
		}
	}

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

	[STNodeProperty("POI模板", "POI模板名称", true)]
	public string POITempName
	{
		get
		{
			return _POITempName;
		}
		set
		{
			_POITempName = value;
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

	[STNodeProperty("颜色", "颜色", true)]
	public CVOLED_COLOR Color
	{
		get
		{
			return _Color;
		}
		set
		{
			_Color = value;
			m_ctrl_color.Value = value;
		}
	}

	[STNodeProperty("缓存大小", "缓存大小", true)]
	public int BufferLen
	{
		get
		{
			return _BufferLen;
		}
		set
		{
			_BufferLen = value;
		}
	}

	private void setTempName()
	{
		m_ctrl_temp.Value = $"{_TempId}:{_TempName}";
	}

	public AlgorithmARVRNode()
		: base("ARVR算法", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "MTF";
		_TempName = "";
		_TempId = -1;
		base.Height += 50;
		_OrderIndex = -1;
		_BufferLen = 1024;
		_Color = CVOLED_COLOR.GREEN;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_editText = CreateControl(typeof(STNodeEditText<AlgorithmARVRType>), m_custom_item, "算法:", _Algorithm);
		m_custom_item.Y += 25;
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "模板:", $"{_TempId}:{_TempName}");
		m_custom_item.Y += 25;
		m_ctrl_color = CreateControl(typeof(STNodeEditText<CVOLED_COLOR>), m_custom_item, "颜色:", _Color);
	}

	private void setAlgorithmType()
	{
		m_ctrl_editText.Value = _Algorithm;
		AlgorithmObjType.instance.algorithmARVRType = _Algorithm;
		switch (_Algorithm)
		{
		case AlgorithmARVRType.MTF:
			operatorCode = "MTF";
			break;
		case AlgorithmARVRType.SFR:
			operatorCode = "SFR";
			break;
		case AlgorithmARVRType.FOV:
			operatorCode = "FOV";
			break;
		case AlgorithmARVRType.畸变:
			operatorCode = "Distortion";
			break;
		case AlgorithmARVRType.双目融合:
			operatorCode = "ARVR.BinocularFusion";
			break;
		case AlgorithmARVRType.SFR_FindROI:
			operatorCode = "ARVR.SFR.FindROI";
			break;
		case AlgorithmARVRType.十字计算:
			operatorCode = "FindCross";
			break;
		}
		base.nodeEvent?.Invoke(this, new FlowEngineNodeEventArgs());
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmParam_ROI algorithmParam_ROI = new AlgorithmParam_ROI();
		algorithmParam_ROI.POITemplateParam = new CVTemplateParam
		{
			ID = -1,
			Name = _POITempName
		};
		algorithmParam_ROI.Color = _Color;
		algorithmParam_ROI.ImgFileName = _ImgFileName;
		algorithmParam_ROI.FileType = GetImageFileType(_ImgFileName);
		algorithmParam_ROI.IsInversion = false;
		algorithmParam_ROI.BufferLen = _BufferLen;
		algorithmParam_ROI.TemplateParam = new CVTemplateParam
		{
			ID = _TempId,
			Name = _TempName
		};
		getPreStepParam(start, algorithmParam_ROI);
		algorithmParam_ROI.SMUData = GetSMUResult(start);
		return algorithmParam_ROI;
	}
}
