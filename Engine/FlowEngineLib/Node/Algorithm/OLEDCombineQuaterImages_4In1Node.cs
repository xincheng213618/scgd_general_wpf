using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using log4net;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

[STNode("/03_5 OLED")]
public class OLEDCombineQuaterImages_4In1Node : CVBaseServerNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(OLEDCombineQuaterImages_4In1Node));

	private CVOLED_COLOR _Color;

	private string _TempName;

	private string _ImgFileName1;

	private string _ImgFileName2;

	private string _ImgFileName3;

	private string _ImgFileName4;

	private string _OutputFileName;

	private STNodeEditText<string> m_ctrl_temp;

	protected CVStartCFC[] masterInput;

	protected STNodeOption m_in_pic2;

	protected STNodeOption m_in_pic3;

	protected STNodeOption m_in_pic4;

	[STNodeProperty("图像文件1", "图像文件1", true)]
	public string ImgFileName1
	{
		get
		{
			return _ImgFileName1;
		}
		set
		{
			_ImgFileName1 = value;
		}
	}

	[STNodeProperty("图像文件2", "图像文件2", true)]
	public string ImgFileName2
	{
		get
		{
			return _ImgFileName2;
		}
		set
		{
			_ImgFileName2 = value;
		}
	}

	[STNodeProperty("图像文件3", "图像文件3", true)]
	public string ImgFileName3
	{
		get
		{
			return _ImgFileName3;
		}
		set
		{
			_ImgFileName3 = value;
		}
	}

	[STNodeProperty("图像文件4", "图像文件4", true)]
	public string ImgFileName4
	{
		get
		{
			return _ImgFileName4;
		}
		set
		{
			_ImgFileName4 = value;
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
		}
	}

	private void setTempName()
	{
		m_ctrl_temp.Value = _TempName;
	}

	public OLEDCombineQuaterImages_4In1Node()
		: base("OLED图像4In1合并", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "OLED.CombineQuaterImages";
		_TempName = "";
		m_in_text = "IMG1";
		_OutputFileName = "result.cvcie";
		masterInput = new CVStartCFC[4];
		base.Height += 30;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_in_pic2 = base.InputOptions.Add("IMG2", typeof(CVStartCFC), bSingle: true);
		m_in_pic2.Connected += m_in_op_Connected;
		m_in_pic2.DataTransfer += m_in_start_DataTransfer;
		m_in_pic3 = base.InputOptions.Add("IMG3", typeof(CVStartCFC), bSingle: true);
		m_in_pic3.Connected += m_in_op_Connected;
		m_in_pic3.DataTransfer += m_in_start_DataTransfer;
		m_in_pic4 = base.InputOptions.Add("IMG4", typeof(CVStartCFC), bSingle: true);
		m_in_pic4.Connected += m_in_op_Connected;
		m_in_pic4.DataTransfer += m_in_start_DataTransfer;
	}

	protected override void m_in_start_DataTransfer(object sender, STNodeOptionEventArgs e)
	{
		DoInputDataTransfer(sender as STNodeOption, e);
	}

	private void DoInputDataTransfer(STNodeOption sender, STNodeOptionEventArgs e)
	{
		if (e.Status != ConnectionStatus.Connected)
		{
			return;
		}
		if (HasData(e))
		{
			bool flag = true;
			if (e.TargetOption.DataType == typeof(CVStartCFC))
			{
				CVStartCFC cVStartCFC = e.TargetOption.Data as CVStartCFC;
				if (cVStartCFC.IsPaused)
				{
					DoTransferToServer(cVStartCFC, e);
					return;
				}
				CVStartCFC data = new CVStartCFC(cVStartCFC);
				sender.Data = data;
				int num = 0;
				StatusTypeEnum statusType = StatusTypeEnum.Runing;
				for (int i = 0; i < base.InputOptionsCount; i++)
				{
					STNodeOption sTNodeOption = base.InputOptions[i];
					if (sTNodeOption.DataType == typeof(CVStartCFC))
					{
						CVStartCFC cVStartCFC2 = (CVStartCFC)sTNodeOption.Data;
						if (cVStartCFC2 != null)
						{
							if (cVStartCFC2.IsRunning)
							{
								flag = flag;
							}
							else
							{
								statusType = cVStartCFC2.FlowStatus;
								flag = !flag && false;
							}
							masterInput[i] = cVStartCFC2;
							num++;
						}
					}
					else
					{
						logger.WarnFormat("TargetData Type is not flow common type => {0}", sTNodeOption.DataType.AssemblyQualifiedName);
					}
				}
				if (logger.IsDebugEnabled)
				{
					logger.DebugFormat("[{0}][{1}/{2}] DoServerTransfer => {3} [{4}/{5}]", ToShortString(), num, base.InputOptionsCount, cVStartCFC.ToShortString(), sender.Text, JsonConvert.SerializeObject(cVStartCFC.Data));
				}
				if (num == base.InputOptionsCount)
				{
					clearData();
					if (flag)
					{
						DoTransferToServer(cVStartCFC, e);
					}
					else
					{
						cVStartCFC.SetStatusType(statusType);
						DoNodeEndedTransferData(cVStartCFC);
					}
					clearInCFC();
				}
			}
			else
			{
				logger.WarnFormat("TargetData Type is not flow common type => {0}", e.TargetOption.DataType.AssemblyQualifiedName);
			}
		}
		else
		{
			clearData();
			clearInCFC();
			DoNodeEndedTransferData(null);
		}
	}

	private void clearInCFC()
	{
		for (int i = 0; i < base.InputOptionsCount; i++)
		{
			masterInput[i] = null;
		}
	}

	private void clearData()
	{
		for (int i = 0; i < base.InputOptionsCount; i++)
		{
			STNodeOption sTNodeOption = base.InputOptions[i];
			if (sTNodeOption.DataType == typeof(CVStartCFC))
			{
				sTNodeOption.Data = null;
			}
		}
	}

	private void DoNodeEndedTransferData(CVStartCFC obj)
	{
		m_op_end.TransferData(obj);
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
