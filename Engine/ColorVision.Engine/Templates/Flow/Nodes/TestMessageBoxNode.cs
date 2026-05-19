using FlowEngineLib;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ColorVision.Engine.Templates.Flow.Nodes
{
    public enum LocalTestExecutionMode
    {
        Success,
        Failed,
        DelaySuccess,
        DelayFailed
    }

    [STNode("/00 全局", "本地处理测试节点")]
    public class TestMessageBoxNode : CVBaseServerNode
    {
        private LocalTestExecutionMode _ExecutionMode;
        private int _DelayMilliseconds;
        private bool _ShowMessageBox;
        private string _MessageText;
        private string _FailureMessage;
        private string _ResultDataKey;
        private string _ResultJson;

        [STNodeProperty("执行模式", "成功、失败、延时成功、延时失败")]
        public LocalTestExecutionMode ExecutionMode
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

        [STNodeProperty("结果Key", "成功时写入流程Data的Key，留空则不写入")]
        public string ResultDataKey
        {
            get => _ResultDataKey;
            set => _ResultDataKey = value;
        }

        [STNodeProperty("结果Json", "成功时写入流程Data的Json内容")]
        public string ResultJson
        {
            get => _ResultJson;
            set => _ResultJson = value;
        }

        public TestMessageBoxNode()
            : base("本地测试", "LocalTest", "LOCAL_TEST", "LOCAL")
        {
            _ExecutionMode = LocalTestExecutionMode.Success;
            _DelayMilliseconds = 1000;
            _ShowMessageBox = true;
            _MessageText = "测试";
            _FailureMessage = "本地测试节点执行失败";
            _ResultDataKey = "LocalTestResult";
            _ResultJson = "{\"message\":\"测试\"}";
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
            return _ExecutionMode == LocalTestExecutionMode.DelaySuccess
                || _ExecutionMode == LocalTestExecutionMode.DelayFailed;
        }

        private bool IsSuccessMode()
        {
            return _ExecutionMode == LocalTestExecutionMode.Success
                || _ExecutionMode == LocalTestExecutionMode.DelaySuccess;
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
                    FinishLocalNode(trans);
                }
                else
                {
                    FailLocalNode(trans);
                }
            }
            finally
            {
                m_trans_action.TryRemove(trans.trans_action.SerialNumber, out _);
            }
        }

        private void FinishLocalNode(CVTransAction trans)
        {
            CVStartCFC action = trans.trans_action;
            object resultData = BuildResultData(action);
            if (!string.IsNullOrWhiteSpace(_ResultDataKey))
            {
                action.Data[_ResultDataKey] = resultData;
            }

            CVServerResponse response = new CVServerResponse(action.SerialNumber, ActionStatusEnum.Finish, "Finish", operatorCode, resultData);
            action.AddResult(GetLocalNodeName(), response, trans.startTime);
            TransferEnd(trans, response, 0);
        }

        private void FailLocalNode(CVTransAction trans)
        {
            CVStartCFC action = trans.trans_action;
            string message = string.IsNullOrWhiteSpace(_FailureMessage) ? "Local node failed" : _FailureMessage;
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

        private object BuildResultData(CVStartCFC start)
        {
            if (!string.IsNullOrWhiteSpace(_ResultJson))
            {
                try
                {
                    return JToken.Parse(_ResultJson);
                }
                catch
                {
                    return _ResultJson;
                }
            }

            return new Dictionary<string, object>
            {
                ["SerialNumber"] = start.SerialNumber,
                ["Message"] = _MessageText
            };
        }

        private string BuildRunPayload(CVStartCFC start)
        {
            return JsonConvert.SerializeObject(new
            {
                ServiceName = NodeName,
                DeviceCode,
                EventName = operatorCode,
                start.SerialNumber,
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
