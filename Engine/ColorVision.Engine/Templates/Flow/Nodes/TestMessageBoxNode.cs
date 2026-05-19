using ColorVision.Database;
using ColorVision.FileIO;
using FlowEngineLib;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Templates.Flow.Nodes
{
    public enum LocalImageExecutionMode
    {
        Success,
        Failed,
        DelaySuccess,
        DelayFailed
    }

    internal sealed class LocalImageResultData
    {
        public object POIResult { get; set; }

        public int TotalTime { get; set; }

        public int MasterId { get; set; }

        public int MasterResultType { get; set; }

        public string MasterValue { get; set; }
    }

    [STNode("/02 相机", "本地图片节点")]
    public class TestMessageBoxNode : CVBaseServerNode
    {
        private const int LocalImageMasterResultType = 100;
        private const int LocalImageTotalTime = 0;

        private LocalImageExecutionMode _ExecutionMode;
        private int _DelayMilliseconds;
        private string _ImageFileUrl;
        private string _FileUrlDataKey;
        private string _BatchName;
        private bool _UseSerialNumberAsBatchName;
        private bool _AutoFileType;
        private int _FileType;
        private string _ParamsJson;
        private string _FrameInfoJson;
        private int _NDPort;
        private int _SmuDataId;
        private int _ResultCode;
        private string _Result;
        private bool _ShowMessageBox;
        private string _MessageText;
        private string _FailureMessage;

        [STNodeProperty("执行模式", "成功、失败、延时成功、延时失败")]
        public LocalImageExecutionMode ExecutionMode
        {
            get => _ExecutionMode;
            set => _ExecutionMode = value;
        }

        [STNodeProperty("延时毫秒", "延时成功或延时失败时等待的毫秒数")]
        public int DelayMilliseconds
        {
            get => _DelayMilliseconds;
            set => _DelayMilliseconds = Math.Max(0, value);
        }

        [STNodeProperty("图片路径", "本地图片文件路径")]
        public string ImageFileUrl
        {
            get => _ImageFileUrl;
            set => _ImageFileUrl = value;
        }

        [STNodeProperty("路径DataKey", "图片路径为空时，从流程Data里读取这个Key")]
        public string FileUrlDataKey
        {
            get => _FileUrlDataKey;
            set => _FileUrlDataKey = value;
        }

        [STNodeProperty("批次名", "按批次Name或Code查询t_scgd_measure_batch，留空默认使用流程流水号")]
        public string BatchName
        {
            get => _BatchName;
            set => _BatchName = value;
        }

        [STNodeProperty("流水号作批次", "优先使用流程SerialNumber作为批次名")]
        public bool UseSerialNumberAsBatchName
        {
            get => _UseSerialNumberAsBatchName;
            set => _UseSerialNumberAsBatchName = value;
        }

        [STNodeProperty("自动文件类型", "根据扩展名生成file_type，cvraw为2，cvcie为1，其它为0")]
        public bool AutoFileType
        {
            get => _AutoFileType;
            set => _AutoFileType = value;
        }

        [STNodeProperty("文件类型", "关闭自动文件类型时写入file_type的值")]
        public int FileType
        {
            get => _FileType;
            set => _FileType = Math.Max(0, Math.Min(sbyte.MaxValue, value));
        }

        [STNodeProperty("相机参数Json", "写入params字段，留空则使用默认相机参数")]
        public string ParamsJson
        {
            get => _ParamsJson;
            set => _ParamsJson = value;
        }

        [STNodeProperty("图像信息Json", "写入file_data字段，留空则尝试从图片文件头读取")]
        public string FrameInfoJson
        {
            get => _FrameInfoJson;
            set => _FrameInfoJson = value;
        }

        [STNodeProperty("NDPort", "写入nd_port字段")]
        public int NDPort
        {
            get => _NDPort;
            set => _NDPort = value;
        }

        [STNodeProperty("SMU数据ID", "写入smu_data_id字段，<=0时写入NULL")]
        public int SmuDataId
        {
            get => _SmuDataId;
            set => _SmuDataId = value;
        }

        [STNodeProperty("结果码", "写入result_code字段")]
        public int ResultCode
        {
            get => _ResultCode;
            set => _ResultCode = value;
        }

        [STNodeProperty("结果", "写入result字段")]
        public string Result
        {
            get => _Result;
            set => _Result = value;
        }

        [STNodeProperty("弹出提示", "执行时是否弹出本地提示框")]
        public bool ShowMessageBox
        {
            get => _ShowMessageBox;
            set => _ShowMessageBox = value;
        }

        [STNodeProperty("提示内容", "本地提示框内容")]
        public string MessageText
        {
            get => _MessageText;
            set => _MessageText = value;
        }

        [STNodeProperty("失败消息", "节点失败时写入流程状态的消息")]
        public string FailureMessage
        {
            get => _FailureMessage;
            set => _FailureMessage = value;
        }

        public TestMessageBoxNode()
            : base("本地图片", "Camera", "SVR.Camera.Default", "DEV.Camera.Default")
        {
            operatorCode = "GetData";
            _MaxTime = 60000;
            _ExecutionMode = LocalImageExecutionMode.Success;
            _DelayMilliseconds = 1000;
            _ImageFileUrl = string.Empty;
            _FileUrlDataKey = "ImageFileUrl";
            _BatchName = string.Empty;
            _UseSerialNumberAsBatchName = true;
            _AutoFileType = true;
            _FileType = 2;
            _ParamsJson = string.Empty;
            _FrameInfoJson = string.Empty;
            _NDPort = -1;
            _SmuDataId = -1;
            _ResultCode = 0;
            _Result = "ok";
            _ShowMessageBox = false;
            _MessageText = "测试";
            _FailureMessage = "本地图片节点执行失败";
        }

        protected override void m_in_start_DataTransfer(object sender, STNodeOptionEventArgs e)
        {
            if (e.Status != ConnectionStatus.Connected || !HasData(e))
            {
                m_op_end.TransferData(e.TargetOption.Data);
                return;
            }

            if (e.TargetOption.Data is not CVStartCFC start)
            {
                m_op_end.TransferData(e.TargetOption.Data);
                return;
            }

            if (!start.IsRunning)
            {
                m_op_end.TransferData(start);
                return;
            }

            CVTransAction trans = new(start);
            m_trans_action.AddOrUpdate(start.SerialNumber, trans, (_, _) => trans);
            nodeRunEvent?.Invoke(this, new FlowEngineNodeRunEventArgs
            {
                SendTopic = "LOCAL",
                SendMsgId = start.SerialNumber,
                SendEventName = operatorCode,
                SendPayload = BuildRunPayload(start)
            });

            if (IsDelayMode())
            {
                Task.Run(async () =>
                {
                    await Task.Delay(_DelayMilliseconds);
                    CompleteLocalNode(trans);
                });
            }
            else
            {
                CompleteLocalNode(trans);
            }
        }

        private bool IsDelayMode()
        {
            return _ExecutionMode == LocalImageExecutionMode.DelaySuccess
                || _ExecutionMode == LocalImageExecutionMode.DelayFailed;
        }

        private bool IsSuccessMode()
        {
            return _ExecutionMode == LocalImageExecutionMode.Success
                || _ExecutionMode == LocalImageExecutionMode.DelaySuccess;
        }

        private void CompleteLocalNode(CVTransAction trans)
        {
            try
            {
                if (_ShowMessageBox)
                {
                    MessageBox.Show(_MessageText, base.Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                if (IsSuccessMode())
                {
                    FinishLocalImageNode(trans);
                }
                else
                {
                    FailLocalNode(trans, _FailureMessage);
                }
            }
            finally
            {
                m_trans_action.TryRemove(trans.trans_action.SerialNumber, out _);
            }
        }

        private void FinishLocalImageNode(CVTransAction trans)
        {
            CVStartCFC action = trans.trans_action;
            string fileUrl = ResolveFileUrl(action);
            if (string.IsNullOrWhiteSpace(fileUrl))
            {
                FailLocalNode(trans, "本地图片路径为空");
                return;
            }

            if (!File.Exists(fileUrl))
            {
                FailLocalNode(trans, $"本地图片不存在: {fileUrl}");
                return;
            }

            string batchName = ResolveBatchName(action);
            MeasureBatchModel batch = BatchResultMasterDao.Instance.GetByNameOrCode(batchName);
            if (batch == null || batch.Id <= 0)
            {
                FailLocalNode(trans, $"未找到批次: {batchName}");
                return;
            }

            MeasureResultImgModel model = BuildMeasureResultImgModel(batch.Id, fileUrl);
            int masterId = MeasureImgResultDao.Instance.SaveAndReturnId(model);
            if (masterId <= 0)
            {
                FailLocalNode(trans, $"写入图片结果失败: {fileUrl}");
                return;
            }

            LocalImageResultData resultData = new()
            {
                POIResult = null,
                TotalTime = LocalImageTotalTime,
                MasterId = masterId,
                MasterResultType = LocalImageMasterResultType,
                MasterValue = null
            };

            action.MasterValue(null, masterId, LocalImageMasterResultType);
            CVServerResponse response = new CVServerResponse(action.SerialNumber, ActionStatusEnum.Finish, "Finish", operatorCode, resultData);
            action.AddResult(GetLocalNodeName(), response, trans.startTime);
            TransferEnd(trans, response, 0);
        }

        private MeasureResultImgModel BuildMeasureResultImgModel(int batchId, string fileUrl)
        {
            return new MeasureResultImgModel
            {
                BatchId = batchId,
                ZIndex = ZIndex,
                NDPort = _NDPort,
                Params = ResolveParamsJson(fileUrl),
                SmuDataId = _SmuDataId > 0 ? _SmuDataId : null,
                IResult = null,
                VResult = null,
                RawFile = Path.GetFileName(fileUrl),
                FileUrl = fileUrl,
                FileType = ResolveFileType(fileUrl),
                ImgFrameInfo = ResolveFrameInfoJson(fileUrl),
                ResultCode = _ResultCode,
                Result = string.IsNullOrWhiteSpace(_Result) ? "ok" : _Result,
                TotalTime = LocalImageTotalTime,
                DeviceCode = DeviceCode,
                CreateDate = DateTime.Now
            };
        }

        private void FailLocalNode(CVTransAction trans, string message)
        {
            CVStartCFC action = trans.trans_action;
            string errorMessage = string.IsNullOrWhiteSpace(message) ? "Local image node failed" : message;
            action.Failed(errorMessage, GetLocalNodeName(), trans.startTime);
            CVServerResponse response = new CVServerResponse(action.SerialNumber, ActionStatusEnum.Failed, errorMessage, operatorCode, null);
            TransferEnd(trans, response, -1);
        }

        private void TransferEnd(CVTransAction trans, CVServerResponse response, int statusCode)
        {
            nodeEndEvent?.Invoke(this, new FlowEngineNodeEndEventArgs
            {
                RecvTopic = "LOCAL",
                RecvMsgId = response.Id,
                RecvEventName = response.EventName,
                RecvStatusCode = statusCode,
                RecvStatusMessage = response.Message,
                RecvPayload = response.Data != null ? JsonConvert.SerializeObject(response.Data) : null
            });
            m_op_end.TransferData(trans.trans_action);
        }

        private string ResolveFileUrl(CVStartCFC start)
        {
            string fileUrl = _ImageFileUrl;
            if (string.IsNullOrWhiteSpace(fileUrl) && !string.IsNullOrWhiteSpace(_FileUrlDataKey))
            {
                start.GetDataValueString(_FileUrlDataKey, ref fileUrl);
            }

            if (string.IsNullOrWhiteSpace(fileUrl))
            {
                return string.Empty;
            }

            fileUrl = fileUrl.Trim();
            try
            {
                return Path.IsPathRooted(fileUrl) ? fileUrl : Path.GetFullPath(fileUrl);
            }
            catch
            {
                return fileUrl;
            }
        }

        private string ResolveBatchName(CVStartCFC start)
        {
            if (_UseSerialNumberAsBatchName && !string.IsNullOrWhiteSpace(start.SerialNumber))
            {
                return start.SerialNumber;
            }

            if (!string.IsNullOrWhiteSpace(_BatchName))
            {
                return _BatchName.Trim();
            }

            string batchName = string.Empty;
            if (start.GetDataValueString("BatchName", ref batchName) && !string.IsNullOrWhiteSpace(batchName))
            {
                return batchName.Trim();
            }

            if (start.GetDataValueString("BatchCode", ref batchName) && !string.IsNullOrWhiteSpace(batchName))
            {
                return batchName.Trim();
            }

            return start.SerialNumber;
        }

        private sbyte ResolveFileType(string fileUrl)
        {
            if (!_AutoFileType)
            {
                return (sbyte)Math.Max(0, Math.Min(sbyte.MaxValue, _FileType));
            }

            string extension = Path.GetExtension(fileUrl).ToLowerInvariant();
            if (extension == ".cvraw") return 2;
            if (extension == ".cvcie") return 1;
            return 0;
        }

        private string ResolveParamsJson(string fileUrl)
        {
            if (!string.IsNullOrWhiteSpace(_ParamsJson))
            {
                return _ParamsJson;
            }

            int bpp = 16;
            if (TryReadColorVisionHeader(fileUrl, out CVCIEFile fileInfo))
            {
                bpp = fileInfo.Bpp;
            }

            return JsonConvert.SerializeObject(new
            {
                Bpp = bpp,
                Gain = 0,
                IsHDR = false,
                NDPort = _NDPort,
                ExpTime = new[] { 80, 80, 80 },
                AvgCount = 1,
                FlipMode = -99,
                POIParam = (object)null,
                Calibration = new { ID = -1, Name = string.Empty },
                IsAutoExpTime = false,
                IsAutoExpWithND = false,
                CamParamTemplate = new { ID = -1, Name = string.Empty },
                AutoExpTimeTemplate = new { ID = -1, Name = string.Empty }
            });
        }

        private string ResolveFrameInfoJson(string fileUrl)
        {
            if (!string.IsNullOrWhiteSpace(_FrameInfoJson))
            {
                return _FrameInfoJson;
            }

            if (TryReadColorVisionHeader(fileUrl, out CVCIEFile fileInfo))
            {
                return JsonConvert.SerializeObject(new
                {
                    bpp = fileInfo.Bpp,
                    width = fileInfo.Cols,
                    height = fileInfo.Rows,
                    channels = fileInfo.Channels
                });
            }

            try
            {
                using FileStream stream = File.OpenRead(fileUrl);
                BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                BitmapFrame frame = decoder.Frames[0];
                int bpp = frame.Format.BitsPerPixel;
                return JsonConvert.SerializeObject(new
                {
                    bpp,
                    width = frame.PixelWidth,
                    height = frame.PixelHeight,
                    channels = EstimateChannels(bpp)
                });
            }
            catch
            {
                return "{}";
            }
        }

        private static bool TryReadColorVisionHeader(string fileUrl, out CVCIEFile fileInfo)
        {
            int index = CVFileUtil.ReadCIEFileHeader(fileUrl, out fileInfo);
            return index > 0;
        }

        private static int EstimateChannels(int bpp)
        {
            if (bpp >= 32) return 4;
            if (bpp >= 24) return 3;
            return 1;
        }

        private string BuildRunPayload(CVStartCFC start)
        {
            return JsonConvert.SerializeObject(new
            {
                ServiceName = NodeName,
                DeviceCode,
                EventName = operatorCode,
                start.SerialNumber,
                FileUrl = ResolveFileUrl(start),
                BatchName = ResolveBatchName(start),
                ExecutionMode = _ExecutionMode.ToString(),
                DelayMilliseconds = IsDelayMode() ? _DelayMilliseconds : 0
            });
        }

        private string GetLocalNodeName()
        {
            return $"{base.Title}.{NodeName}";
        }
    }
}
