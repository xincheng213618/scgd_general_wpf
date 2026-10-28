using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ProjectARVRPro.Process;
using ProjectARVRPro.Services;
using System.IO;

namespace ProjectARVRPro;

public partial class ARVRWindow
{
    private readonly CancellationTokenSource _luxLifetime = new();
    private LuxCommandSession? _luxSession;
    private ProcessMeta? _luxFlowMeta;
    private TemplateModel<FlowParam>? _luxFlowTemplate;

    internal string? HandleLuxRequest(LuxCommandSession session)
    {
        Dispatcher.VerifyAccess();
        LuxRequest request = session.Request;
        if (request.SocketCode == "00")
        {
            // A station may handshake while another measurement is finishing. Never reset that run.
            if (!IsTestExecutionBusy) ProjectConfig.SN = request.SerialNumber;
            return request.ResponsePrefix;
        }
        if (_isDisposed || IsTestExecutionBusy)
        {
            log.Info($"测试忙碌，忽略 LUX SocketCode={request.SocketCode}");
            return null; // The original protocol has no BUSY response.
        }

        _luxSession = session;
        ProjectConfig.SN = request.SerialNumber;
        UpdateTestButtonState();
        if (request.SocketCode is "01" or "31") _ = ExecuteLuxDeviceRequestAsync(session);
        else _ = StartLuxFlowAsync(session);
        return null;
    }

    private async Task StartLuxFlowAsync(LuxCommandSession session)
    {
        try
        {
            _luxFlowMeta = ProcessManager.FindProcessMetaBySocketCode(session.Request.SocketCode);
            _luxFlowTemplate = _luxFlowMeta?.FindFlowTemplate();
            if (_luxFlowMeta == null || _luxFlowTemplate == null)
            {
                log.Error($"未找到 LUX SocketCode={session.Request.SocketCode} 对应的流程或模板");
                session.Complete(session.Request.ResponsePrefix);
                ReleaseLuxSession(session);
                return;
            }

            ProjectConfig.SN = session.Request.SerialNumber;
            CurrentTestType = ProcessMetas.IndexOf(_luxFlowMeta);
            FlowTemplate.SelectedValue = _luxFlowTemplate.Value;
            await RunLuxTemplateAsync();
        }
        catch (Exception ex)
        {
            log.Error("启动 LUX 流程失败", ex);
            if (ReferenceEquals(_luxSession, session))
                CompleteLuxFlow("Failed", ex.Message);
        }
    }

    private async Task RunLuxTemplateAsync()
    {
        LuxCommandSession? session = _luxSession;
        if (session == null || _luxFlowTemplate == null || _luxFlowMeta == null) return;
        bool started = await TryRunTemplate(_luxFlowTemplate, _luxFlowMeta, cancellationToken: _luxLifetime.Token);
        if (!started && ReferenceEquals(_luxSession, session))
            CompleteLuxFlow("Failed", CurrentFlowResult?.Msg);
    }

    private void CompleteLuxFlow(string? eventName, string? parameters)
    {
        LuxCommandSession? session = _luxSession;
        if (session == null) return;
        try
        {
            if (eventName == "Completed")
            {
                try
                {
                    string directory = ViewResultManager.Config.CsvSavePath;
                    Directory.CreateDirectory(directory);
                    ObjectiveTestResultCsvExporter.ExportToCsv(ObjectiveTestResult,
                        Path.Combine(directory, Tool.SanitizeFileName($"C_{session.Request.SerialNumber}.csv")));
                }
                catch (Exception ex) { log.Error("LUX CSV 导出失败", ex); }
            }
            FinalizeObjectiveTestResultRecord();
            session.Complete(eventName == "Completed"
                ? session.Request.CompletedResponse(ObjectiveTestResult.OpticCenterTestResult)
                : session.Request.FailureResponse(eventName, parameters));
        }
        catch (Exception ex) { log.Error("LUX 流程回包失败", ex); }
        finally { ReleaseLuxSession(session); }
    }

    private async Task ExecuteLuxDeviceRequestAsync(LuxCommandSession session)
    {
        // Freeze the settings with the originating connection, including while a new handshake arrives.
        string directory = ViewResultManager.Config.CsvSavePath;
        double min = ProjectConfig.LuxVidMin, max = ProjectConfig.LuxVidMax;
        double factor = ProjectConfig.LuxVidFix, offset = ProjectConfig.LuxVidB;
        try
        {
            Directory.CreateDirectory(directory);
            if (session.Request.SocketCode == "01")
            {
                double? measured = await LuxDeviceMeasurements.MeasureVidAsync(_luxLifetime.Token);
                double value = measured.HasValue ? measured.Value * factor + offset : 0;
                if (!double.IsFinite(value)) throw new InvalidOperationException("VID 结果不是有效数值。");
                var item = new ObjectiveTestItem { Name = "VID", Unit = "cd/m^2", Value = value, LowLimit = min, UpLimit = max };
                string row = $"2pixel_linePair,{item.Name},{item.Value},{item.Unit},{item.LowLimit},{item.UpLimit},{(item.TestResult ? "pass" : "fail")}";
                File.WriteAllLines(Path.Combine(directory, Tool.SanitizeFileName($"B_{session.Request.SerialNumber}.csv")),
                    [ProjectARVRResultCsvExporter.Header, row]);
                session.Complete(session.Request.MeasurementResponse(value));
            }
            else
            {
                var (succeeded, spectrum) = await LuxDeviceMeasurements.MeasureFluxAsync(_luxLifetime.Token);
                if (spectrum != null)
                    SpectrumCsvExportHelper.ExportLuminousFluxMode(Path.Combine(directory, Tool.SanitizeFileName($"D_{session.Request.SerialNumber}.csv")), [spectrum]);
                session.Complete(session.Request.MeasurementResponse(succeeded ? spectrum?.LuminousFlux : 0));
            }
        }
        catch (OperationCanceledException) when (_luxLifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            log.Error("LUX 设备测量失败", ex);
            try { session.Complete(session.Request.MeasurementResponse(0)); }
            catch (Exception sendError) { log.Error("LUX 设备回包失败", sendError); }
        }
        finally { ReleaseLuxSession(session); }
    }

    private void ReleaseLuxSession(LuxCommandSession session)
    {
        if (!ReferenceEquals(_luxSession, session)) return;
        _luxSession = null;
        _luxFlowMeta = null;
        _luxFlowTemplate = null;
        UpdateTestButtonState();
    }
}
