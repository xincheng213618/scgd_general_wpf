using ColorVision.Themes.Controls;
using cvColorVision;
using Spectrum.Configs;
using Spectrum.Data;
using System.Collections.ObjectModel;
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
                MessageBox1.Show("正在运行");
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
                MessageBox1.Show(Application.Current.GetActiveWindow(), "正在运行");
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
                            MessageBox.Show(Application.Current.GetActiveWindow(), "校零成功");
                        });
                    }
                    else
                    {
                        string errorMsg = Spectrometer.GetErrorMessage(ret);
                        log.Error($"校零失败: {errorMsg}");
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(Application.Current.GetActiveWindow(), $"校零失败: {errorMsg}");
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
                        MessageBox.Show(Application.Current.GetActiveWindow(), "校零异常: " + ex.Message);
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

            if (Manager.EnableAutodark)
            {
                if (!Manager.ShutterController.IsConnected)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), "未配备shutter，无法自动校零", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                    IsRun = false;
                    return;
                }
                log.Debug("开启快门");
                await Manager.ShutterController.OpenShutter();
                int ret = Spectrometer.CM_Emission_DarkStorage(SpectrometerHandle, Manager.IntTime, Manager.Average, 0, Manager.fDarkData);
                log.Debug("关闭快门");
                await Manager.ShutterController.CloseShutter();
                if (ret == 1)
                    log.Debug("测量前自动校零成功");
                else
                    log.Warn($"测量前自动校零失败: {Spectrometer.GetErrorMessage(ret)}");
            }

            if (Manager.EnableAutoIntegration)
            {

                if (Manager.IntTimeConfig.IsOldVersion)
                {
                    ret = Spectrometer.CM_Emission_GetAutoTime(SpectrometerHandle, ref fIntTime, Manager.IntTimeConfig.IntLimitTime, Manager.IntTimeConfig.AutoIntTimeB, (int)Manager.IntTimeConfig.MaxPercent);
                    if (ret == 1)
                    {
                        log.Debug($"自动积分时间: {fIntTime}ms");
                        // When sync frequency is enabled, defer updating Manager.IntTime
                        // to avoid showing intermediate value before sync adjustment
                        if (!Manager.GetDataConfig.IsSyncFrequencyEnabled)
                            Manager.IntTime = fIntTime;
                    }
                    else
                    {
                        log.Warn($"自动积分时间获取失败: {Spectrometer.GetErrorMessage(ret)}");
                        IsRun = false;
                        return;
                    }
                }
                else
                {
                    ret = Spectrometer.CM_Emission_GetAutoTimeEx(SpectrometerHandle, ref fIntTime, Manager.IntTimeConfig.IntLimitTime, Manager.IntTimeConfig.AutoIntTimeB, Manager.IntTimeConfig.Max, MyAutoTimeCallback);
                    if (ret == 1)
                    {
                        log.Debug($"自动积分时间: {fIntTime}ms");
                        if (!Manager.GetDataConfig.IsSyncFrequencyEnabled)
                            Manager.IntTime = fIntTime;
                    }
                    else
                    {
                        log.Warn($"自动积分时间获取失败: {Spectrometer.GetErrorMessage(ret)}");
                        IsRun = false;
                        return;
                    }
                }
            }


            if (Manager.EnableAdaptiveAutoDark)
            {
                // When auto-integration deferred Manager.IntTime update (sync frequency enabled),
                // use the field-level fIntTime which has the fresh auto-integration result
                float darkIntTime = (Manager.EnableAutoIntegration && Manager.GetDataConfig.IsSyncFrequencyEnabled) ? fIntTime : Manager.IntTime;
                ret = Spectrometer.CM_Emission_AutoDarkStorage(SpectrometerHandle, darkIntTime, Manager.Average, 0, Manager.fDarkData);
                if (ret == 1)
                {
                    log.Debug("自适应校零数据获取成功");
                }
                else if (ret == 0)
                {
                    log.Warn("自适应校零未初始化，请先执行一次自适应校零");
                    isstartAuto = false;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("请先做一次自适应校零");
                    });
                    IsRun = false;
                    return;
                }
                else
                {
                    log.Warn($"自适应校零数据获取失败: {Spectrometer.GetErrorMessage(ret)}");
                }
            }

            float fDx = 0;
            float fDy = 0;
            COLOR_PARA cOLOR_PARA = new COLOR_PARA();

            if (Manager.GetDataConfig.IsSyncFrequencyEnabled)
            {
                // Use field-level fIntTime (auto-integration result) when auto-integration ran,
                // otherwise use Manager.IntTime (user-set value)
                float syncIntTime = Manager.EnableAutoIntegration ? fIntTime : Manager.IntTime;
                ret = Spectrometer.CM_Emission_GetDataSyncfreq(SpectrometerHandle, 0, Manager.GetDataConfig.Syncfreq, Manager.GetDataConfig.SyncfreqFactor, ref syncIntTime, Manager.Average, Manager.GetDataConfig.FilterBW, Manager.fDarkData, fDx, fDy, Manager.GetDataConfig.SetWL1, Manager.GetDataConfig.SetWL2, ref cOLOR_PARA);
                if (ret != 1)
                    log.Warn($"同步频率采集数据失败: {Spectrometer.GetErrorMessage(ret)}");

                if (Manager.EnableAutoIntegration)
                    Manager.IntTime = syncIntTime;
            }
            else
            {
                ret = Spectrometer.CM_Emission_GetData(SpectrometerHandle, 0, Manager.IntTime, Manager.Average, Manager.GetDataConfig.FilterBW, Manager.fDarkData, fDx, fDy, Manager.GetDataConfig.SetWL1, Manager.GetDataConfig.SetWL2, ref cOLOR_PARA);
                if (ret == -13007)
                {
                    log.Warn($"采集数据超时，正在重试: {Spectrometer.GetErrorMessage(ret)}");
                    ret = Spectrometer.CM_Emission_GetData(SpectrometerHandle, 0, Manager.IntTime, Manager.Average, Manager.GetDataConfig.FilterBW, Manager.fDarkData, fDx, fDy, Manager.GetDataConfig.SetWL1, Manager.GetDataConfig.SetWL2, ref cOLOR_PARA);
                }
                if (ret != 1)
                    log.Warn($"采集光谱数据失败: {Spectrometer.GetErrorMessage(ret)}");
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
                Application.Current.Dispatcher.Invoke(() =>
                {
                    SprectrumModel sprectrumModel = new SprectrumModel() { ColorParam = cOLOR_PARA };
                    ViewResultManager.Save(sprectrumModel);
                    if (MainWindowConfig.Instance.EqeEnabled && ViewResultSpectrums.Count > 0)
                    {
                        // When SMU is connected, read V/I from it; otherwise use manual values
                        float voltage = MainWindowConfig.Instance.EqeVoltage;
                        float currentMA = MainWindowConfig.Instance.EqeCurrentMA;
                        if (Manager.SmuController.IsOpen)
                        {
                            Manager.SmuController.ApplySettings();
                            if (Manager.SmuController.MeasureData())
                            {
                                var (smuV, smuI) = Manager.SmuController.GetVI();
                                voltage = smuV;
                                currentMA = smuI;
                                MainWindowConfig.Instance.EqeVoltage = voltage;
                                MainWindowConfig.Instance.EqeCurrentMA = currentMA;
                            }
                        }

                        var latest = ViewResultManager.Config.OrderByType == SqlSugar.OrderByType.Desc
                            ? ViewResultSpectrums.FirstOrDefault()
                            : ViewResultSpectrums.LastOrDefault();
                        if (latest != null)
                        {
                            latest.CalculateEqeParams(voltage, currentMA);
                            ViewResultManager.UpdateEqeFields(latest, isRecalculated: false);
                        }
                    }
                });
            }
            else
            {
                errornum++;
                log.Error($"光谱数据采集失败: {Spectrometer.GetErrorMessage(ret)}");
            }
            IsRun = false;
        }

        //单次测量
        private void Button5_Click(object sender, RoutedEventArgs e)
        {
            if (IsRun)
            {
                MessageBox.Show("正在执行任务请稍后");
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
                MessageBox.Show("正在执行任务请稍后");
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
                        MessageBox.Show("自适应校零成功");
                    });
                }
                else
                {
                    string errorMsg = Spectrometer.GetErrorMessage(ret);
                    log.Error($"自适应校零失败: {errorMsg}");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"自适应校零失败: {errorMsg}");
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
                        MessageBox.Show(Application.Current.GetActiveWindow(), "未配备shutter，无法自动校零", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                            MessageBox.Show(Application.Current.MainWindow, $"连续测试执行完毕,执行失败{errornum}");
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
