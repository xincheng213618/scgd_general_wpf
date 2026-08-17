using cvColorVision;
using Newtonsoft.Json;
using Spectrum.Data;
using Spectrum.Models;
using System.Diagnostics;

namespace Spectrum
{
    public sealed record SpectrumMeasurementResult(
        ViewResultSpectrum? Result,
        int? ErrorCode = null,
        string? ErrorMessage = null,
        bool IsBusy = false)
    {
        public bool IsSuccess => Result != null;
    }

    public partial class SpectrometerManager
    {
        public const int OperationBusy = int.MinValue;

        public async Task<(bool Entered, float? IntegrationTime)> TryGetAutoIntegrationTimeAsync(CancellationToken cancellationToken = default)
        {
            var operation = await TryRunExclusiveAsync(
                token => Task.Run<float?>(() =>
                {
                    token.ThrowIfCancellationRequested();
                    if (!IsConnected || Handle == IntPtr.Zero)
                        return null;

                    (int returnCode, float integrationTime) = GetAutoIntegrationTimeCore();
                    if (returnCode != 1)
                    {
                        log.Warn($"自动积分时间获取失败: {Spectrometer.GetErrorMessage(returnCode)}");
                        return null;
                    }

                    if (GetDataConfig.IsSyncFrequencyEnabled)
                    {
                        COLOR_PARA colorParam = new();
                        float synchronizedTime = integrationTime;
                        int syncResult = Spectrometer.CM_Emission_GetDataSyncfreq(
                            Handle, 0, GetDataConfig.Syncfreq, GetDataConfig.SyncfreqFactor,
                            ref synchronizedTime, Average, GetDataConfig.FilterBW, fDarkData,
                            0, 0, GetDataConfig.SetWL1, GetDataConfig.SetWL2, ref colorParam);
                        if (syncResult == 1)
                            integrationTime = synchronizedTime;
                        else
                            log.Warn($"同步频率调整积分时间失败: {Spectrometer.GetErrorMessage(syncResult)}");
                    }

                    IntTime = integrationTime;
                    return integrationTime;
                }, CancellationToken.None),
                cancellationToken).ConfigureAwait(false);

            return (operation.Entered, operation.Result);
        }

        public async Task<int> PerformAdaptiveDarkCalibrationAsync(CancellationToken cancellationToken = default)
        {
            var operation = await TryRunExclusiveAsync(
                token => Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    if (!IsConnected || Handle == IntPtr.Zero)
                        return -1;
                    return Spectrometer.CM_Emission_Init_Auto_Dark(
                        Handle, AutodarkParam.fTimeStart, AutodarkParam.nStepTime,
                        AutodarkParam.nStepCount, Average);
                }, CancellationToken.None),
                cancellationToken).ConfigureAwait(false);

            return operation.Entered ? operation.Result : OperationBusy;
        }

        public async Task<SpectrumMeasurementResult> MeasureAsync(CancellationToken cancellationToken = default)
        {
            if (!TryStartMeasurement())
                return new SpectrumMeasurementResult(null, ErrorMessage: "光谱测量服务正在停止", IsBusy: true);

            try
            {
                return await MeasureTrackedAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                FinishMeasurement();
            }
        }

        private async Task<SpectrumMeasurementResult> MeasureTrackedAsync(CancellationToken cancellationToken)
        {
            Stopwatch totalStopwatch = Stopwatch.StartNew();
            SpectrumMeasurementProfile profile = new()
            {
                CreateTime = DateTime.Now,
                MeasurementMode = GetDataConfig.IsSyncFrequencyEnabled ? "sync-frequency" : "standard",
                InputParametersJson = CreateMeasurementInputSnapshotJson()
            };
            bool operationStarted = false;

            SpectrumMeasurementResult Failure(int? code, string message)
            {
                profile.ErrorCode = code;
                profile.ErrorMessage = message;
                log.Warn(message);
                return new SpectrumMeasurementResult(null, code, message);
            }

            try
            {
                var operation = await TryRunExclusiveAsync(
                    token => Task.Run(async () =>
                    {
                        operationStarted = true;
                        return await CaptureMeasurementCoreAsync(profile, token).ConfigureAwait(false);
                    }, CancellationToken.None),
                    cancellationToken).ConfigureAwait(false);

                if (!operation.Entered)
                    return new SpectrumMeasurementResult(null, ErrorMessage: "光谱仪正在执行其他操作", IsBusy: true);

                MeasurementCapture capture = operation.Result
                    ?? new MeasurementCapture(null, null, null, null, "测量未返回结果");
                if (!capture.ColorParam.HasValue)
                    return Failure(capture.ErrorCode, capture.ErrorMessage ?? "测量失败");

                cancellationToken.ThrowIfCancellationRequested();
                Stopwatch persistStopwatch = Stopwatch.StartNew();
                SprectrumModel model = new()
                {
                    ColorParam = capture.ColorParam.Value,
                    TotalDurationMs = totalStopwatch.ElapsedMilliseconds
                };
                ViewResultSpectrum viewResult = ViewResultManager.Save(model, capture.EqeVoltage, capture.EqeCurrent);
                profile.PersistDurationMs = persistStopwatch.ElapsedMilliseconds;
                profile.SpectrumId = model.Id;
                profile.IsSuccess = true;
                return new SpectrumMeasurementResult(viewResult);
            }
            catch (OperationCanceledException)
            {
                profile.ErrorMessage = "测量已取消";
                throw;
            }
            catch (Exception ex)
            {
                log.Error("光谱测量异常", ex);
                return Failure(null, ex.GetBaseException().Message);
            }
            finally
            {
                totalStopwatch.Stop();
                profile.TotalDurationMs = totalStopwatch.ElapsedMilliseconds;
                if (operationStarted)
                {
                    try
                    {
                        ViewResultManager.SaveMeasurementProfile(profile);
                        log.Info($"测量耗时: total={profile.TotalDurationMs}ms, autoDark={profile.AutoDarkDurationMs ?? 0}ms, autoIntegration={profile.AutoIntegrationDurationMs ?? 0}ms, adaptiveDark={profile.AdaptiveAutoDarkDurationMs ?? 0}ms, acquire={profile.AcquireDurationMs ?? 0}ms, persist={profile.PersistDurationMs ?? 0}ms, success={profile.IsSuccess}, spectrumId={profile.SpectrumId?.ToString() ?? "-"}");
                    }
                    catch (Exception ex)
                    {
                        log.Error("保存测量耗时记录失败", ex);
                    }
                }
            }
        }

        private async Task<MeasurementCapture> CaptureMeasurementCoreAsync(SpectrumMeasurementProfile profile, CancellationToken cancellationToken)
        {
            MeasurementCapture Failure(int? code, string message)
            {
                profile.ErrorCode = code;
                profile.ErrorMessage = message;
                return new MeasurementCapture(null, null, null, code, message);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!IsConnected || Handle == IntPtr.Zero)
                return Failure(null, "光谱仪未连接");

            if (EnableAutodark)
            {
                if (!ShutterController.IsConnected)
                    return Failure(null, Properties.Resources.NoShutterAutoZero);

                Stopwatch stepStopwatch = Stopwatch.StartNew();
                int darkResult;
                await ShutterController.CloseShutter().ConfigureAwait(false);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    darkResult = Spectrometer.CM_Emission_DarkStorage(Handle, IntTime, Average, 0, fDarkData);
                }
                finally
                {
                    await ShutterController.OpenShutter().ConfigureAwait(false);
                }

                profile.AutoDarkDurationMs = stepStopwatch.ElapsedMilliseconds;
                if (darkResult != 1)
                    log.Warn($"测量前自动校零失败，继续使用现有暗场数据: {Spectrometer.GetErrorMessage(darkResult)}");
            }

            float integrationTime = IntTime;
            if (EnableAutoIntegration)
            {
                Stopwatch stepStopwatch = Stopwatch.StartNew();
                (int returnCode, float value) = GetAutoIntegrationTimeCore();
                profile.AutoIntegrationDurationMs = stepStopwatch.ElapsedMilliseconds;
                if (returnCode != 1)
                    return Failure(returnCode, $"自动积分时间获取失败: {Spectrometer.GetErrorMessage(returnCode)}");

                integrationTime = value;
                if (!GetDataConfig.IsSyncFrequencyEnabled)
                    IntTime = integrationTime;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (EnableAdaptiveAutoDark)
            {
                float darkIntegrationTime = EnableAutoIntegration && GetDataConfig.IsSyncFrequencyEnabled ? integrationTime : IntTime;
                Stopwatch stepStopwatch = Stopwatch.StartNew();
                int adaptiveDarkResult = Spectrometer.CM_Emission_AutoDarkStorage(Handle, darkIntegrationTime, Average, 0, fDarkData);
                profile.AdaptiveAutoDarkDurationMs = stepStopwatch.ElapsedMilliseconds;
                if (adaptiveDarkResult == 0)
                    return Failure(adaptiveDarkResult, Properties.Resources.PleaseRunAdaptiveAutoDarkFirst);
                if (adaptiveDarkResult != 1)
                    log.Warn($"自适应校零数据获取失败，继续测量: {Spectrometer.GetErrorMessage(adaptiveDarkResult)}");
            }

            cancellationToken.ThrowIfCancellationRequested();
            COLOR_PARA colorParam = new();
            Stopwatch acquireStopwatch = Stopwatch.StartNew();
            int acquireResult;
            if (GetDataConfig.IsSyncFrequencyEnabled)
            {
                float syncIntegrationTime = EnableAutoIntegration ? integrationTime : IntTime;
                acquireResult = Spectrometer.CM_Emission_GetDataSyncfreq(
                    Handle, 0, GetDataConfig.Syncfreq, GetDataConfig.SyncfreqFactor,
                    ref syncIntegrationTime, Average, GetDataConfig.FilterBW, fDarkData,
                    0, 0, GetDataConfig.SetWL1, GetDataConfig.SetWL2, ref colorParam);
                if (acquireResult == 1 && EnableAutoIntegration)
                    IntTime = syncIntegrationTime;
            }
            else
            {
                acquireResult = Spectrometer.CM_Emission_GetData(
                    Handle, 0, IntTime, Average, GetDataConfig.FilterBW, fDarkData,
                    0, 0, GetDataConfig.SetWL1, GetDataConfig.SetWL2, ref colorParam);
                if (acquireResult == -13007)
                {
                    log.Warn($"采集数据超时，正在重试: {Spectrometer.GetErrorMessage(acquireResult)}");
                    acquireResult = Spectrometer.CM_Emission_GetData(
                        Handle, 0, IntTime, Average, GetDataConfig.FilterBW, fDarkData,
                        0, 0, GetDataConfig.SetWL1, GetDataConfig.SetWL2, ref colorParam);
                }
            }

            profile.AcquireDurationMs = acquireStopwatch.ElapsedMilliseconds;
            if (acquireResult != 1)
                return Failure(acquireResult, $"光谱数据采集失败: {Spectrometer.GetErrorMessage(acquireResult)}");

            colorParam.fPh = colorParam.fPh < 1 ? (float)Math.Round(colorParam.fPh, 4) : (float)Math.Round(colorParam.fPh, 2);

            float? eqeVoltage = null;
            float? eqeCurrent = null;
            if (MainWindowConfig.Instance.EqeEnabled)
            {
                eqeVoltage = MainWindowConfig.Instance.EqeVoltage;
                eqeCurrent = MainWindowConfig.Instance.EqeCurrentMA;
                if (SmuController.IsOpen && !SmuController.IsBusy && SmuController.CaptureMeasurementSnapshot() is { } snapshot)
                {
                    SmuController.ApplyMeasurement(snapshot);
                    eqeVoltage = snapshot.Voltage;
                    eqeCurrent = snapshot.CurrentMA;
                    MainWindowConfig.Instance.EqeVoltage = snapshot.Voltage;
                    MainWindowConfig.Instance.EqeCurrentMA = snapshot.CurrentMA;
                }
            }

            return new MeasurementCapture(colorParam, eqeVoltage, eqeCurrent, null, null);
        }

        private sealed record MeasurementCapture(
            COLOR_PARA? ColorParam,
            float? EqeVoltage,
            float? EqeCurrent,
            int? ErrorCode,
            string? ErrorMessage);

        private (int ReturnCode, float Value) GetAutoIntegrationTimeCore()
        {
            float integrationTime = 0;
            int result = IntTimeConfig.IsOldVersion
                ? Spectrometer.CM_Emission_GetAutoTime(Handle, ref integrationTime, IntTimeConfig.IntLimitTime, IntTimeConfig.AutoIntTimeB, (int)IntTimeConfig.MaxPercent)
                : Spectrometer.CM_Emission_GetAutoTimeEx(Handle, ref integrationTime, IntTimeConfig.IntLimitTime, IntTimeConfig.AutoIntTimeB, IntTimeConfig.Max, null);
            return (result, integrationTime);
        }

        private string CreateMeasurementInputSnapshotJson()
        {
            return JsonConvert.SerializeObject(new
            {
                RequestedIntTime = IntTime,
                Average,
                GetDataConfig.FilterBW,
                EnableAutodark,
                EnableAdaptiveAutoDark,
                EnableAutoIntegration,
                GetDataConfig.IsSyncFrequencyEnabled,
                GetDataConfig.Syncfreq,
                GetDataConfig.SyncfreqFactor,
                GetDataConfig.SetWL1,
                GetDataConfig.SetWL2
            });
        }
    }
}
