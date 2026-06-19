#pragma warning disable CA1805,CA1822,CA1859,CA1863,CS0414
using ColorVision.Themes.Controls;
using cvColorVision;
using Newtonsoft.Json;
using Spectrum.Data;
using SpectrumResources = Spectrum.Properties.Resources;
using System.Diagnostics;
using System.Windows;

namespace Spectrum
{
    public partial class MainWindow
    {
        float fIntTime = 0;
        int testid = 0;
        int ret;
        bool IsRun;
        int errornum = 0;
        bool isstartAuto;

        public int MyAutoTimeCallback(int time, double spectum)
        {
            log.Debug($"自动积分时间回调: 积分时间={time}, 光谱强度={spectum}");
            return 0;
        }

        private void AutoIntTime_Click(object sender, RoutedEventArgs e)
        {
            if (IsRun)
            {
                MessageBox1.Show(SpectrumResources.OperationInProgressPleaseWait);
                return;
            }
            SetOperationButtonsEnabled(false);

            Task.Run(() =>
            {
                IsRun = true;
                if (Manager.IntTimeConfig.IsOldVersion)
                {
                    ret = Spectrometer.CM_Emission_GetAutoTime(SpectrometerHandle, ref fIntTime, Manager.IntTimeConfig.IntLimitTime, Manager.IntTimeConfig.AutoIntTimeB, (int)Manager.IntTimeConfig.MaxPercent);
                    if (ret == 1)
                    {
                        log.Info($"自动积分时间获取成功: {fIntTime}ms");
                    }
                    else
                    {
                        log.Warn($"自动积分时间获取失败: {Spectrometer.GetErrorMessage(ret)}");
                    }
                }
                else
                {
                    ret = Spectrometer.CM_Emission_GetAutoTimeEx(SpectrometerHandle, ref fIntTime, Manager.IntTimeConfig.IntLimitTime, Manager.IntTimeConfig.AutoIntTimeB, Manager.IntTimeConfig.Max, MyAutoTimeCallback);
                    if (ret == 1)
                    {
                        log.Info($"自动积分时间获取成功: {fIntTime}ms");
                    }
                    else
                    {
                        log.Warn($"自动积分时间获取失败: {Spectrometer.GetErrorMessage(ret)}");
                    }
                }

                // Apply sync frequency adjustment if enabled
                if (ret == 1 && Manager.GetDataConfig.IsSyncFrequencyEnabled)
                {
                    float syncIntTime = fIntTime;
                    COLOR_PARA cOLOR_PARA = new COLOR_PARA();
                    int syncRet = Spectrometer.CM_Emission_GetDataSyncfreq(SpectrometerHandle, 0, Manager.GetDataConfig.Syncfreq, Manager.GetDataConfig.SyncfreqFactor, ref syncIntTime, Manager.Average, Manager.GetDataConfig.FilterBW, Manager.fDarkData, 0, 0, Manager.GetDataConfig.SetWL1, Manager.GetDataConfig.SetWL2, ref cOLOR_PARA);
                    if (syncRet == 1)
                    {
                        log.Info($"同步频率调整积分时间: {fIntTime}ms → {syncIntTime}ms");
                        fIntTime = syncIntTime;
                    }
                    else
                    {
                        log.Warn($"同步频率调整积分时间失败: {Spectrometer.GetErrorMessage(syncRet)}");
                    }
                }

                if (ret == 1)
                {
                    Manager.IntTime = fIntTime;
                }

                IsRun = false;
                SetOperationButtonsEnabled(true);
            });


        }

        //单次校零
        private void Button3_Click(object sender, RoutedEventArgs e)
        {
            if (IsRun)
            {
                MessageBox1.Show(Application.Current.GetActiveWindow(), SpectrumResources.OperationInProgressPleaseWait);
                return;
            }
            IsRun = true;
            SetOperationButtonsEnabled(false);

            Task.Run(async () =>
            {
                try
                {
                    if (Manager.ShutterController.IsConnected)
                    {
                        log.Debug("开启快门");
                       await  Manager.ShutterController.OpenShutter();
                    }
                    int ret = Spectrometer.CM_Emission_DarkStorage(SpectrometerHandle, Manager.IntTime, Manager.Average, 0, Manager.fDarkData);
                    if (Manager.ShutterController.IsConnected)
                    {
                        log.Debug("关闭快门");
                        await Manager.ShutterController.CloseShutter();
                    }
                    if (ret == 1)
                    {
                        log.Info("校零成功");
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(Application.Current.GetActiveWindow(), SpectrumResources.ZeroCalibrationSuccess);
                        });
                    }
                    else
                    {
                        string errorMsg = Spectrometer.GetErrorMessage(ret);
                        log.Error($"校零失败: {errorMsg}");
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(Application.Current.GetActiveWindow(), string.Format(SpectrumResources.ZeroCalibrationFailed, errorMsg));
                        });
                    }
                    IsRun = false;
                    SetOperationButtonsEnabled(true);
                }
                catch (Exception ex)
                {
                    log.Error("校零异常", ex);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), string.Format(SpectrumResources.ZeroCalibrationException, ex.Message));
                    });
                    IsRun = false;
                    SetOperationButtonsEnabled(true);
                }
            });


        }

        public async Task Measure()
        {
            if (IsRun)
            {
                log.Info("上次执行还未结束");
                return;
            }
            IsRun = true;

            var totalStopwatch = Stopwatch.StartNew();
            var stepDetails = new List<MeasurementStepDetail>();
            var profile = new SpectrumMeasurementProfile
            {
                CreateTime = DateTime.Now,
                MeasurementMode = Manager.GetDataConfig.IsSyncFrequencyEnabled ? "sync-frequency" : "standard",
                InputParametersJson = CreateMeasurementInputSnapshotJson()
            };
            long? committedTotalDurationMs = null;

            try
            {
                if (Manager.EnableAutodark)
                {
                    if (!Manager.ShutterController.IsConnected)
                    {
                        string message = SpectrumResources.NoShutterAutoZero;
                        AddMeasurementStep(stepDetails, "AutoDarkPrerequisite", 0, false, null, new
                        {
                            Manager.EnableAutodark,
                            ShutterConnected = false
                        }, message);
                        profile.ErrorMessage = message;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(Application.Current.GetActiveWindow(), message, SpectrumResources.PromptTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                        });
                        return;
                    }

                    var autoDarkStopwatch = Stopwatch.StartNew();
                    log.Debug("开启快门");
                    await Manager.ShutterController.OpenShutter();
                    int autoDarkRet = Spectrometer.CM_Emission_DarkStorage(SpectrometerHandle, Manager.IntTime, Manager.Average, 0, Manager.fDarkData);
                    log.Debug("关闭快门");
                    await Manager.ShutterController.CloseShutter();
                    autoDarkStopwatch.Stop();

                    profile.AutoDarkDurationMs = autoDarkStopwatch.ElapsedMilliseconds;
                    string autoDarkMessage = autoDarkRet == 1 ? "测量前自动校零成功" : Spectrometer.GetErrorMessage(autoDarkRet);
                    AddMeasurementStep(stepDetails, "AutoDark", profile.AutoDarkDurationMs.Value, autoDarkRet == 1, autoDarkRet, new
                    {
                        IntTime = Manager.IntTime,
                        Manager.Average,
                        DarkChannel = 0,
                        ShutterConnected = true
                    }, autoDarkMessage);

                    if (autoDarkRet == 1)
                        log.Debug("测量前自动校零成功");
                    else
                        log.Warn($"测量前自动校零失败: {autoDarkMessage}");
                }

                if (Manager.EnableAutoIntegration)
                {
                    var autoIntegrationStopwatch = Stopwatch.StartNew();
                    if (Manager.IntTimeConfig.IsOldVersion)
                    {
                        ret = Spectrometer.CM_Emission_GetAutoTime(SpectrometerHandle, ref fIntTime, Manager.IntTimeConfig.IntLimitTime, Manager.IntTimeConfig.AutoIntTimeB, (int)Manager.IntTimeConfig.MaxPercent);
                    }
                    else
                    {
                        ret = Spectrometer.CM_Emission_GetAutoTimeEx(SpectrometerHandle, ref fIntTime, Manager.IntTimeConfig.IntLimitTime, Manager.IntTimeConfig.AutoIntTimeB, Manager.IntTimeConfig.Max, MyAutoTimeCallback);
                    }
                    autoIntegrationStopwatch.Stop();

                    profile.AutoIntegrationDurationMs = autoIntegrationStopwatch.ElapsedMilliseconds;
                    string? autoIntegrationError = ret == 1 ? null : Spectrometer.GetErrorMessage(ret);
                    AddMeasurementStep(stepDetails, "AutoIntegration", profile.AutoIntegrationDurationMs.Value, ret == 1, ret, new
                    {
                        Manager.IntTimeConfig.IsOldVersion,
                        Manager.IntTimeConfig.IntLimitTime,
                        Manager.IntTimeConfig.AutoIntTimeB,
                        AutoIntegrationMax = Manager.IntTimeConfig.IsOldVersion ? (object)Manager.IntTimeConfig.MaxPercent : Manager.IntTimeConfig.Max
                    }, ret == 1 ? $"自动积分时间={fIntTime}ms" : autoIntegrationError);

                    if (ret == 1)
                    {
                        log.Debug($"自动积分时间: {fIntTime}ms");
                        if (!Manager.GetDataConfig.IsSyncFrequencyEnabled)
                            Manager.IntTime = fIntTime;
                    }
                    else
                    {
                        profile.ErrorCode = ret;
                        profile.ErrorMessage = $"自动积分时间获取失败: {autoIntegrationError}";
                        log.Warn(profile.ErrorMessage);
                        return;
                    }
                }

                if (Manager.EnableAdaptiveAutoDark)
                {
                    float darkIntTime = (Manager.EnableAutoIntegration && Manager.GetDataConfig.IsSyncFrequencyEnabled) ? fIntTime : Manager.IntTime;
                    var adaptiveAutoDarkStopwatch = Stopwatch.StartNew();
                    ret = Spectrometer.CM_Emission_AutoDarkStorage(SpectrometerHandle, darkIntTime, Manager.Average, 0, Manager.fDarkData);
                    adaptiveAutoDarkStopwatch.Stop();

                    profile.AdaptiveAutoDarkDurationMs = adaptiveAutoDarkStopwatch.ElapsedMilliseconds;
                    string adaptiveAutoDarkMessage = ret == 1
                        ? SpectrumResources.AdaptiveAutoDarkSuccess
                        : ret == 0
                            ? SpectrumResources.PleaseRunAdaptiveAutoDarkFirst
                            : Spectrometer.GetErrorMessage(ret);

                    AddMeasurementStep(stepDetails, "AdaptiveAutoDark", profile.AdaptiveAutoDarkDurationMs.Value, ret == 1, ret, new
                    {
                        IntTime = darkIntTime,
                        Manager.Average,
                        DarkChannel = 0
                    }, adaptiveAutoDarkMessage);

                    if (ret == 1)
                    {
                        log.Debug("自适应校零数据获取成功");
                    }
                    else if (ret == 0)
                    {
                        profile.ErrorCode = ret;
                        profile.ErrorMessage = adaptiveAutoDarkMessage;
                        log.Warn(adaptiveAutoDarkMessage);
                        isstartAuto = false;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(SpectrumResources.PleaseRunAdaptiveAutoDarkFirst);
                        });
                        return;
                    }
                    else
                    {
                        log.Warn($"自适应校零数据获取失败: {adaptiveAutoDarkMessage}");
                    }
                }

                float fDx = 0;
                float fDy = 0;
                COLOR_PARA cOLOR_PARA = new COLOR_PARA();

                int acquireAttempts = 1;
                var acquireStopwatch = Stopwatch.StartNew();
                if (Manager.GetDataConfig.IsSyncFrequencyEnabled)
                {
                    float requestedIntTime = Manager.EnableAutoIntegration ? fIntTime : Manager.IntTime;
                    float syncIntTime = requestedIntTime;
                    ret = Spectrometer.CM_Emission_GetDataSyncfreq(SpectrometerHandle, 0, Manager.GetDataConfig.Syncfreq, Manager.GetDataConfig.SyncfreqFactor, ref syncIntTime, Manager.Average, Manager.GetDataConfig.FilterBW, Manager.fDarkData, fDx, fDy, Manager.GetDataConfig.SetWL1, Manager.GetDataConfig.SetWL2, ref cOLOR_PARA);
                    acquireStopwatch.Stop();

                    profile.AcquireDurationMs = acquireStopwatch.ElapsedMilliseconds;
                    string acquireMessage = ret == 1 ? $"同步频率采集成功，积分时间={syncIntTime}ms" : Spectrometer.GetErrorMessage(ret);
                    AddMeasurementStep(stepDetails, "AcquireDataSyncFrequency", profile.AcquireDurationMs.Value, ret == 1, ret, new
                    {
                        RequestedIntTime = requestedIntTime,
                        FinalIntTime = syncIntTime,
                        Manager.GetDataConfig.Syncfreq,
                        Manager.GetDataConfig.SyncfreqFactor,
                        Manager.Average,
                        Manager.GetDataConfig.FilterBW,
                        Manager.GetDataConfig.SetWL1,
                        Manager.GetDataConfig.SetWL2
                    }, acquireMessage);

                    if (ret != 1)
                        log.Warn($"同步频率采集数据失败: {acquireMessage}");

                    if (Manager.EnableAutoIntegration)
                        Manager.IntTime = syncIntTime;
                }
                else
                {
                    float requestedIntTime = Manager.IntTime;
                    ret = Spectrometer.CM_Emission_GetData(SpectrometerHandle, 0, Manager.IntTime, Manager.Average, Manager.GetDataConfig.FilterBW, Manager.fDarkData, fDx, fDy, Manager.GetDataConfig.SetWL1, Manager.GetDataConfig.SetWL2, ref cOLOR_PARA);
                    if (ret == -13007)
                    {
                        acquireAttempts = 2;
                        log.Warn($"采集数据超时，正在重试: {Spectrometer.GetErrorMessage(ret)}");
                        ret = Spectrometer.CM_Emission_GetData(SpectrometerHandle, 0, Manager.IntTime, Manager.Average, Manager.GetDataConfig.FilterBW, Manager.fDarkData, fDx, fDy, Manager.GetDataConfig.SetWL1, Manager.GetDataConfig.SetWL2, ref cOLOR_PARA);
                    }
                    acquireStopwatch.Stop();

                    profile.AcquireDurationMs = acquireStopwatch.ElapsedMilliseconds;
                    string acquireMessage = ret == 1 ? "采集光谱数据成功" : Spectrometer.GetErrorMessage(ret);
                    AddMeasurementStep(stepDetails, "AcquireData", profile.AcquireDurationMs.Value, ret == 1, ret, new
                    {
                        RequestedIntTime = requestedIntTime,
                        Manager.Average,
                        Manager.GetDataConfig.FilterBW,
                        Manager.GetDataConfig.SetWL1,
                        Manager.GetDataConfig.SetWL2,
                        Attempts = acquireAttempts
                    }, acquireMessage);

                    if (ret != 1)
                        log.Warn($"采集光谱数据失败: {acquireMessage}");
                }

                if (ret == 1)
                {
                    if (cOLOR_PARA.fPh < 1)
                    {
                        cOLOR_PARA.fPh = (float)Math.Round((float)cOLOR_PARA.fPh, 4);
                    }
                    else
                    {
                        cOLOR_PARA.fPh = (float)Math.Round((float)cOLOR_PARA.fPh, 2);
                    }

                    var smuMeasurement = MainWindowConfig.Instance.EqeEnabled && Manager.SmuController.IsOpen && !Manager.SmuController.IsBusy
                        ? Manager.SmuController.CaptureMeasurementSnapshot()
                        : null;

                    long renderDurationMs = 0;
                    long persistDurationMs = 0;

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var renderStopwatch = Stopwatch.StartNew();
                        SprectrumModel sprectrumModel = new SprectrumModel()
                        {
                            ColorParam = cOLOR_PARA,
                            TotalDurationMs = totalStopwatch.ElapsedMilliseconds
                        };
                        renderStopwatch.Stop();
                        renderDurationMs = renderStopwatch.ElapsedMilliseconds;

                        var persistStopwatch = Stopwatch.StartNew();
                        ViewResultManager.Save(sprectrumModel);

                        var latest = ViewResultManager.Config.OrderByType == SqlSugar.OrderByType.Desc
                            ? ViewResultSpectrums.FirstOrDefault()
                            : ViewResultSpectrums.LastOrDefault();

                        if (MainWindowConfig.Instance.EqeEnabled && latest != null)
                        {
                            float voltage = MainWindowConfig.Instance.EqeVoltage;
                            float currentMA = MainWindowConfig.Instance.EqeCurrentMA;
                            if (smuMeasurement.HasValue)
                            {
                                Manager.SmuController.ApplyMeasurement(smuMeasurement.Value);
                                voltage = smuMeasurement.Value.Voltage;
                                currentMA = smuMeasurement.Value.CurrentMA;
                                MainWindowConfig.Instance.EqeVoltage = voltage;
                                MainWindowConfig.Instance.EqeCurrentMA = currentMA;
                            }

                            latest.CalculateEqeParams(voltage, currentMA);
                            ViewResultManager.UpdateEqeFields(latest, isRecalculated: false);
                        }

                        persistStopwatch.Stop();
                        persistDurationMs = persistStopwatch.ElapsedMilliseconds;

                        long currentTotalDurationMs = totalStopwatch.ElapsedMilliseconds;
                        ViewResultManager.UpdateMeasurementDuration(sprectrumModel.Id, currentTotalDurationMs);
                        if (latest != null)
                            latest.TotalDurationMs = currentTotalDurationMs;

                        profile.SpectrumId = sprectrumModel.Id;
                        committedTotalDurationMs = currentTotalDurationMs;
                    });

                    profile.RenderDurationMs = renderDurationMs;
                    profile.PersistDurationMs = persistDurationMs;
                    AddMeasurementStep(stepDetails, "RenderResult", renderDurationMs, true, null, new
                    {
                        HasEqeMeasurement = smuMeasurement.HasValue,
                        MainWindowConfig.Instance.EqeEnabled
                    }, "生成视图结果");
                    AddMeasurementStep(stepDetails, "PersistResult", persistDurationMs, true, 1, new
                    {
                        profile.SpectrumId,
                        MainWindowConfig.Instance.EqeEnabled
                    }, "保存测量结果");
                    profile.IsSuccess = true;
                }
                else
                {
                    errornum++;
                    string errorMessage = Spectrometer.GetErrorMessage(ret);
                    profile.ErrorCode = ret;
                    profile.ErrorMessage = errorMessage;
                    log.Error($"光谱数据采集失败: {errorMessage}");
                }
            }
            finally
            {
                totalStopwatch.Stop();
                profile.TotalDurationMs = committedTotalDurationMs ?? totalStopwatch.ElapsedMilliseconds;
                SaveMeasurementProfile(profile, stepDetails);
                IsRun = false;
            }
        }

        private string CreateMeasurementInputSnapshotJson()
        {
            return JsonConvert.SerializeObject(new
            {
                RequestedIntTime = Manager.IntTime,
                Manager.Average,
                Manager.GetDataConfig.FilterBW,
                Manager.EnableAutodark,
                Manager.EnableAdaptiveAutoDark,
                Manager.EnableAutoIntegration,
                Manager.GetDataConfig.IsSyncFrequencyEnabled,
                Manager.GetDataConfig.Syncfreq,
                Manager.GetDataConfig.SyncfreqFactor,
                Manager.GetDataConfig.SetWL1,
                Manager.GetDataConfig.SetWL2,
                Manager.IntTimeConfig.IsOldVersion,
                Manager.IntTimeConfig.IntLimitTime,
                Manager.IntTimeConfig.AutoIntTimeB,
                AutoIntegrationMax = Manager.IntTimeConfig.IsOldVersion ? (object)Manager.IntTimeConfig.MaxPercent : Manager.IntTimeConfig.Max,
                ShutterConnected = Manager.ShutterController.IsConnected
            });
        }

        private static void AddMeasurementStep(ICollection<MeasurementStepDetail> stepDetails, string stepName, long durationMs, bool isSuccess, int? returnCode = null, object? input = null, string? message = null)
        {
            stepDetails.Add(new MeasurementStepDetail
            {
                StepName = stepName,
                DurationMs = durationMs,
                IsSuccess = isSuccess,
                ReturnCode = returnCode,
                InputJson = input == null ? null : JsonConvert.SerializeObject(input),
                Message = message
            });
        }

        private void SaveMeasurementProfile(SpectrumMeasurementProfile profile, IList<MeasurementStepDetail> stepDetails)
        {
            try
            {
                profile.StepDetailsJson = JsonConvert.SerializeObject(stepDetails);
                ViewResultManager.SaveMeasurementProfile(profile);
                log.Info($"测量耗时统计: total={profile.TotalDurationMs}ms, autoDark={profile.AutoDarkDurationMs ?? 0}ms, autoIntegration={profile.AutoIntegrationDurationMs ?? 0}ms, adaptiveDark={profile.AdaptiveAutoDarkDurationMs ?? 0}ms, acquire={profile.AcquireDurationMs ?? 0}ms, render={profile.RenderDurationMs ?? 0}ms, persist={profile.PersistDurationMs ?? 0}ms, success={profile.IsSuccess}, spectrumId={profile.SpectrumId?.ToString() ?? "-"}");
                log.Debug($"测量耗时明细: {profile.StepDetailsJson}");
            }
            catch (Exception ex)
            {
                log.Error("保存测量耗时记录失败", ex);
            }
        }

        //单次测量
        private void Button5_Click(object sender, RoutedEventArgs e)
        {
            if (IsRun)
            {
                MessageBox.Show(SpectrumResources.OperationInProgressPleaseWait);
                return;
            }
            SetOperationButtonsEnabled(false);
            Task.Run(async () =>
            {
                try
                {
                    await Measure();
                }
                finally
                {
                    SetOperationButtonsEnabled(true);
                }
            });
        }

        /// <summary>
        /// Disables/enables all C++ operation buttons to prevent concurrent spectrometer calls.
        /// Must be called on the UI thread.
        /// </summary>
        private void SetOperationButtonsEnabled(bool enabled)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                button3.IsEnabled = enabled;
                button5.IsEnabled = enabled;
                button6.IsEnabled = enabled;
                ButtonAutoInt.IsEnabled = enabled;
            });
        }
        //自适应校零
        private void Button4_Click_1(object sender, RoutedEventArgs e)
        {
           if (IsRun)
            {
                MessageBox.Show(SpectrumResources.OperationInProgressPleaseWait);
                return;
            }
            log.Debug("开始自适应校零");
            SetOperationButtonsEnabled(false);
            Task.Run(() =>
            {
                IsRun = true;
                int ret = Spectrometer.CM_Emission_Init_Auto_Dark(SpectrometerHandle, Manager.AutodarkParam.fTimeStart, Manager.AutodarkParam.nStepTime, Manager.AutodarkParam.nStepCount, Manager.Average);
                IsRun = false;
                SetOperationButtonsEnabled(true);
                if (ret == 1)
                {
                    log.Info("自适应校零成功");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(SpectrumResources.AdaptiveAutoDarkSuccess);
                    });
                }
                else
                {
                    string errorMsg = Spectrometer.GetErrorMessage(ret);
                    log.Error($"自适应校零失败: {errorMsg}");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(string.Format(SpectrumResources.AdaptiveAutoDarkFailed, errorMsg));
                    });
                }
            });
        }


        //连续测量
        private void Button6_Click(object sender, RoutedEventArgs e)
        {
            IsRun = false;
            isstartAuto = true;
            errornum = 0;
            button6.Visibility = Visibility.Collapsed;
            button7.Visibility = Visibility.Visible;
            ContinuousProgressBar.Value = 0;
            TimeEstimationPanel.Visibility = Visibility.Visible;
            ElapsedTimeText.Text = "--:--";
            RemainingTimeText.Text = "--:--";
            // Disable other operation buttons during continuous testing
            button3.IsEnabled = false;
            button5.IsEnabled = false;
            ButtonAutoInt.IsEnabled = false;
            if (Manager.EnableAutodark)
            {
                if (!Manager.ShutterController.IsConnected)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), SpectrumResources.NoShutterAutoZero, SpectrumResources.PromptTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                        button6.Visibility = Visibility.Visible;
                        button7.Visibility = Visibility.Collapsed;
                        ContinuousProgressBar.Value = 100;
                        TimeEstimationPanel.Visibility = Visibility.Collapsed;
                        Manager.LoopMeasureNum = 0;
                        errornum = 0;
                        // Re-enable operation buttons
                        button3.IsEnabled = true;
                        button5.IsEnabled = true;
                        button6.IsEnabled = true;
                        ButtonAutoInt.IsEnabled = true;
                    });
                    IsRun = false;
                    return;
                }
            }

            Task.Run(()=> LoopMeasure());
        }
        public async void LoopMeasure()
        {
            log.Info($"LoopMeasure Start All Count {Manager.MeasurementNum}");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (isstartAuto)
            {
                if (Manager.MeasurementNum > 0)
                {
                    if (Manager.LoopMeasureNum >= Manager.MeasurementNum)
                    {

                        isstartAuto = false;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            button6.Visibility = Visibility.Visible;
                            button7.Visibility = Visibility.Collapsed;
                            ContinuousProgressBar.Value = 100;
                            TimeEstimationPanel.Visibility = Visibility.Collapsed;
                            Manager.LoopMeasureNum = 0;
                            errornum = 0;
                            // Re-enable operation buttons
                            button3.IsEnabled = true;
                            button5.IsEnabled = true;
                            button6.IsEnabled = true;
                            ButtonAutoInt.IsEnabled = true;
                            MessageBox.Show(Application.Current.MainWindow, string.Format(SpectrumResources.ContinuousTestCompletedWithFailureCount, errornum));
                        });
                        break;
                    }
                    Manager.LoopMeasureNum++;

                    // Update progress bar and time estimation
                    int current = Manager.LoopMeasureNum;
                    int total = Manager.MeasurementNum;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        double progress = (double)current / total * 100;
                        ContinuousProgressBar.Value = progress;

                        var elapsed = stopwatch.Elapsed;
                        ElapsedTimeText.Text = FormatTimeSpan(elapsed);

                        if (current > 0)
                        {
                            double avgPerItem = elapsed.TotalSeconds / current;
                            double remainingSeconds = avgPerItem * (total - current);
                            RemainingTimeText.Text = FormatTimeSpan(TimeSpan.FromSeconds(remainingSeconds));
                        }
                    });
                }
                await Measure();
                await Task.Delay(Manager.MeasurementInterval);
            }
        }

        private static string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            return $"{ts.Minutes:D2}:{ts.Seconds:D2}";
        }

        //停止连续测量
        private void Button7_Click(object sender, RoutedEventArgs e)
        {
            isstartAuto = false;
            button6.Visibility = Visibility.Visible;
            button7.Visibility = Visibility.Collapsed;
            TimeEstimationPanel.Visibility = Visibility.Collapsed;
            Manager.LoopMeasureNum = 0;
            // Re-enable operation buttons
            button3.IsEnabled = true;
            button5.IsEnabled = true;
            button6.IsEnabled = true;
            ButtonAutoInt.IsEnabled = true;
        }
    }
}
