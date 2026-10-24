#nullable disable
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;
using System.Collections.Generic;
using System.Linq;

namespace FlowEngineLib;

[STNode("关注点", CategoryOrder = 310)]
[STNodeSerializationModel("FlowEngineLib.dll|FlowEngineLib.POINode")]
[FlowNodeDocumentation("按 POI 模板测量图像；上游相机使用本地后端时复用本地 POI 节点，否则使用原算法服务。",
    Notes = "本地分支读取当前内存帧或带校正参数的 RAW，不要求生成 CVCIE 文件；过滤、修正、亚像素和文件输出设置保留但不参与本地测量。")]
public class POINode : CVBaseServerNode
{
	public override bool RequiresRemoteService => !UsesLocalCamera();

	protected override FlowLocalExecution CreateLocalExecution(CVMQTTRequest request)
	{
		if (!UsesLocalCamera()) return null;
		POIDataParam data = (POIDataParam)request.Data;
		return new LocalPoiNode { POITempName = data.TemplateParam?.Name ?? string.Empty }
			.CreateCompatibilityExecution(request, data.ImgFileName);
	}

	private bool UsesLocalCamera()
	{
		HashSet<STNode> visited = new();
		STNode[] current = GetAllInputOptions().Take(1).SelectMany(option => option.GetConnectedOption()).Select(option => option.Owner).ToArray();
		while (current.Length > 0)
		{
			List<STNode> next = new();
			Dictionary<string, bool> cameras = new();
			foreach (STNode upstream in current)
			{
				if (!visited.Add(upstream)) continue;
				if (upstream is LocalCameraNode localCamera)
				{
					cameras[localCamera.DeviceCode] = true;
					continue;
				}
				if (upstream is CVBaseServerNode { NodeType: "Camera" } camera)
				{
					DeviceCamera device = ServiceManager.Current?.DeviceServices.OfType<DeviceCamera>()
						.FirstOrDefault(item => item.Code == camera.DeviceCode);
					if (device == null) return false;
					cameras[device.Code] = device.CameraBackend.OpensLocally;
					continue;
				}
				next.AddRange(upstream.GetAllInputOptions().SelectMany(option => option.GetConnectedOption()).Select(option => option.Owner));
			}
			if (cameras.Count > 0) return cameras.Count == 1 && cameras.Values.Single();
			current = next.ToArray();
		}
		return false;
	}

	private string _FilterTemplateName;

	private string _ReviseTemplateName;

	private string _OutputTemplateName;

	private bool _IsSubPixel;

	private STNodeEditText<string> m_ctrl_filtertemp;

	private STNodeEditText<string> m_ctrl_outtemp;

	[STNodeProperty("参数模板", "参数模板", true)]
	[System.ComponentModel.PropertyEditorTypeAttribute(typeof(PoiTemplatePropertiesEditor))]
	public string TempName
	{
		get
		{
			return _TempName;
		}
		set
		{
			setTempName(value);
			OnPropertyChanged();
		}
	}

	[STNodeProperty("过滤模板", "过滤模板", true)]
	[System.ComponentModel.PropertyEditorTypeAttribute(typeof(PoiFilterTemplatePropertiesEditor))]
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
			OnPropertyChanged();
		}
	}

	[STNodeProperty("修正模板", "修正模板", true)]
	[System.ComponentModel.PropertyEditorTypeAttribute(typeof(PoiReviseTemplatePropertiesEditor))]
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
			OnPropertyChanged();
		}
	}

	[STNodeProperty("文件输出模板", "文件输出模板", true)]
	[System.ComponentModel.PropertyEditorTypeAttribute(typeof(PoiOutputTemplatePropertiesEditor))]
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
			OnPropertyChanged();
		}
	}

	[STNodeProperty("图像文件", "图像文件", true)]
	[System.ComponentModel.DataAnnotations.Display(Order = -100)]
	[System.ComponentModel.PropertyEditorTypeAttribute(typeof(System.ComponentModel.TextSelectFilePropertiesEditor))]
	public string ImgFileName
	{
		get
		{
			return _ImgFileName;
		}
		set
		{
			_ImgFileName = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("色温/波长", "色温/波长", true, false, true)]
	[System.ComponentModel.ReadOnly(true)]
	[System.ComponentModel.PropertyEditorType(typeof(System.ComponentModel.TextboxPropertiesEditor))]
	public bool IsCCTWave
	{
		get => true;
		// Keep the setter for old STN records; their saved value cannot disable measurement.
		set { }
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
			OnPropertyChanged();
		}
	}

	public POINode()
		: base("关注点算法", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "POI";
		_FilterTemplateName = "";
		_ReviseTemplateName = "";
		_OutputTemplateName = "";
		_ImgFileName = "";
		_IsSubPixel = false;
		base.Height += 50;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		base.InputOptions.Add(STNodeOption.Empty);
		base.InputOptions.Add(STNodeOption.Empty);
		CreateTempControl(m_custom_item);
		m_custom_item.Y += 25;
		m_ctrl_filtertemp = CreateStringControl(m_custom_item, "F/R:", getFilterReviseTemp());
		m_custom_item.Y += 25;
		m_ctrl_outtemp = CreateStringControl(m_custom_item, "输出:", _OutputTemplateName);
	}

	private void setFilterReviseTemp()
	{
		m_ctrl_filtertemp.Value = getFilterReviseTemp();
	}

	private string getFilterReviseTemp()
	{
		return $"{_FilterTemplateName}/{_ReviseTemplateName}";
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmPreStepParam param = new AlgorithmPreStepParam();
		getPreStepParam(start, param);
		POIDataParam pOIDataParam = new POIDataParam(_FilterTemplateName, _ReviseTemplateName, _OutputTemplateName, param, _IsSubPixel, IsCCTWave);
		BuildImageParam(pOIDataParam);
		pOIDataParam.SMUData = GetSMUResult(start);
		return pOIDataParam;
	}
}
