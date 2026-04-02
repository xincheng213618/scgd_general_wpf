#pragma warning disable CS8602,CS8603,CS8601
using log4net;
using Quartz;
using System;
using System.Threading.Tasks;
using System.Windows;


namespace ColorVision.Engine.Templates.Flow.Job
{
    /// <summary>
    /// FlowJob 执行结果，存入 IJobExecutionContext.Result 供 TaskExecutionListener 读取
    /// </summary>
    public class FlowJobResult
    {
        public bool Success { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public long TotalTimeMs { get; set; }

        public override string ToString()
        {
            string text = $"{Status} | 耗时: {TotalTimeMs}ms";
            if (!string.IsNullOrEmpty(Message))
                text += $" | {Message}";
            return text;
        }
    }

    public class FlowJob : IJob
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FlowJob));

        public async Task Execute(IJobExecutionContext context)
        {
            log.Info($"FlowJob 开始执行: {context.JobDetail.Key.Name}");

            FlowControlData? result = null;

            try
            {
                // 在 UI 线程中启动流程并获取等待任务
                Task<FlowControlData?> flowTask = await Application.Current.Dispatcher.InvokeAsync(
                    () => FlowEngineManager.GetInstance().DisplayFlow.RunFlowAndWaitAsync()
                );

                // 等待流程执行完成（可在任意线程上等待）
                result = await flowTask;
            }
            catch (Exception ex)
            {
                log.Error("FlowJob 启动流程异常", ex);
                context.Result = new FlowJobResult
                {
                    Success = false,
                    Status = "启动异常",
                    Message = ex.Message
                };
                return;
            }

            if (result == null)
            {
                log.Warn("FlowJob 流程未能启动（验证失败或正在运行中）");
                context.Result = new FlowJobResult
                {
                    Success = false,
                    Status = "未启动",
                    Message = "流程未能启动：可能验证失败、流程正在运行中或未选中模板"
                };
                return;
            }

            bool success = result.FlowStatus == FlowStatus.Completed;
            context.Result = new FlowJobResult
            {
                Success = success,
                Status = result.EventName,
                Message = result.Params,
                TotalTimeMs = result.TotalTime
            };

            log.Info($"FlowJob 流程完成: {result.EventName}, 耗时: {result.TotalTime}ms, 成功: {success}");
        }
    }
}
