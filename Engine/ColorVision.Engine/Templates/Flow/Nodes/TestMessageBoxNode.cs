using ColorVision.Database;
using ColorVision.FileIO;
using FlowEngineLib;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Templates.Flow.Nodes
{
    internal sealed class LocalImageResultData
    {
        public object? POIResult { get; set; }

        public int TotalTime { get; set; }

        public int MasterId { get; set; }

        public int MasterResultType { get; set; }

        public string? MasterValue { get; set; }
    }

    [STNode("/02 相机", "本地图片节点")]
    public class TestMessageBoxNode : CVBaseServerNode
    {
        private const int LocalImageMasterResultType = 100;
        private const int LocalImageTotalTime = 0;
        private const int DefaultNdPort = -1;
        private const int DefaultResultCode = 0;
        private const string DefaultResult = "ok";

        private string _ImageFileUrl;

        [Display(Name = "Engine_PG_FilePath", GroupName = "Engine_PG_LocalImage", ResourceType = typeof(Properties.Resources))]
        [PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
        [STNodeProperty("文件路径", "本地图片文件路径", true)]
        public string ImageFileUrl
        {
            get => _ImageFileUrl;
            set => _ImageFileUrl = value;
        }

        public TestMessageBoxNode()
            : base("本地图片", "Camera", "SVR.Camera.Default", "DEV.Camera.Default")
        {
            operatorCode = "GetData";
            _MaxTime = 60000;
            _ImageFileUrl = string.Empty;
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

            try
            {
                FinishLocalImageNode(trans);
            }
            finally
            {
                m_trans_action.TryRemove(trans.trans_action.SerialNumber, out _);
            }
        }

        private void FinishLocalImageNode(CVTransAction trans)
        {
            CVStartCFC action = trans.trans_action;
            string fileUrl = ResolveFileUrl();
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

            string batchName = action.SerialNumber;
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
                NDPort = DefaultNdPort,
                Params = ResolveParamsJson(fileUrl),
                SmuDataId = null,
                IResult = null,
                VResult = null,
                RawFile = Path.GetFileName(fileUrl),
                FileUrl = fileUrl,
                FileType = ResolveFileType(fileUrl),
                ImgFrameInfo = ResolveFrameInfoJson(fileUrl),
                ResultCode = DefaultResultCode,
                Result = DefaultResult,
                TotalTime = LocalImageTotalTime,
                DeviceCode = DeviceCode,
                CreateDate = DateTime.Now
            };
        }

        private void FailLocalNode(CVTransAction trans, string message)
        {
            CVStartCFC action = trans.trans_action;
            action.Failed(message, GetLocalNodeName(), trans.startTime);
            CVServerResponse response = new CVServerResponse(action.SerialNumber, ActionStatusEnum.Failed, message, operatorCode, null);
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

        private string ResolveFileUrl()
        {
            if (string.IsNullOrWhiteSpace(_ImageFileUrl))
            {
                return string.Empty;
            }

            string fileUrl = _ImageFileUrl.Trim();
            try
            {
                return Path.IsPathRooted(fileUrl) ? fileUrl : Path.GetFullPath(fileUrl);
            }
            catch
            {
                return fileUrl;
            }
        }

        private static sbyte ResolveFileType(string fileUrl)
        {
            string extension = Path.GetExtension(fileUrl).ToLowerInvariant();
            if (extension == ".cvraw") return 2;
            if (extension == ".cvcie") return 1;
            return 0;
        }

        private static string ResolveParamsJson(string fileUrl)
        {
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
                NDPort = DefaultNdPort,
                ExpTime = new[] { 80, 80, 80 },
                AvgCount = 1,
                FlipMode = -99,
                POIParam = (object?)null,
                Calibration = new { ID = -1, Name = string.Empty },
                IsAutoExpTime = false,
                IsAutoExpWithND = false,
                CamParamTemplate = new { ID = -1, Name = string.Empty },
                AutoExpTimeTemplate = new { ID = -1, Name = string.Empty }
            });
        }

        private static string ResolveFrameInfoJson(string fileUrl)
        {
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
                FileUrl = ResolveFileUrl()
            });
        }

        private string GetLocalNodeName()
        {
            return $"{base.Title}.{NodeName}";
        }
    }
}
