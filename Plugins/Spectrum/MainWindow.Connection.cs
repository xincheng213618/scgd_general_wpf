using cvColorVision;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace Spectrum
{
    public partial class MainWindow
    {
        public static int MyCallback(IntPtr strText, int nLen)
        {
            string text = Marshal.PtrToStringAnsi(strText, nLen);
            log.Debug("光谱仪回调: " + text);
            return 0;
        }
        public IntPtr SpectrometerHandle => Manager.Handle;

        //连接光谱仪
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Manager.Handle = Spectrometer.CM_CreateEmission(0, MyCallback);

                int com = 0;
                if (Manager.Config.IsComPort)
                {
                     com = int.Parse(Manager.Config.SzComName.Replace("COM", ""));
                }

                int iR = Spectrometer.CM_Emission_Init(SpectrometerHandle, com, Manager.Config.BaudRate);
                if (iR == 1)
                {
                    log.Info("光谱仪连接成功");
                    Manager.IsConnected = true;

                    try
                    {
                        int bufferLength = 1024;
                        StringBuilder snBuilder = new StringBuilder(bufferLength);
                        int snRet = Spectrometer.CM_GetSpectrSerialNumber(SpectrometerHandle, snBuilder);
                        if (snRet == 1)
                        {
                            string sn = snBuilder.ToString().Trim();
                            if (!string.IsNullOrEmpty(sn))
                            {
                                Manager.SerialNumber = sn;
                                log.Info($"光谱仪序列号: {sn}");
                            }
                            else
                            {
                                Manager.SerialNumber = "Unknown";
                            }
                        }
                        else
                        {
                            log.Warn($"获取序列号失败: {Spectrometer.GetErrorMessage(snRet)}");
                            Manager.SerialNumber = "Unknown";
                        }
                    }
                    catch (Exception snEx)
                    {
                        log.Warn("读取序列号异常", snEx);
                        Manager.SerialNumber = "Unknown";
                    }
                    Manager.LoadCalibrationConfig();

                    iR = Spectrometer.CM_Emission_LoadWavaLengthFile(SpectrometerHandle, Manager.WavelengthFile);
                    if (iR == 1)
                        log.Info($"加载波长文件成功: {Manager.WavelengthFile}");
                    else
                        log.Warn($"加载波长文件失败: {Manager.WavelengthFile}, {Spectrometer.GetErrorMessage(iR)}");

                    iR = Spectrometer.CM_Emission_LoadMagiudeFile(SpectrometerHandle, Manager.MaguideFile);
                    if (iR == 1)
                        log.Info($"加载幅值文件成功: {Manager.MaguideFile}");
                    else
                        log.Warn($"加载幅值文件失败: {Manager.MaguideFile}, {Spectrometer.GetErrorMessage(iR)}");

                    log.Debug($"设置 SP100 参数: IsEnabled={SetEmissionSP100Config.Instance.IsEnabled}, nStartPos={SetEmissionSP100Config.Instance.nStartPos}, nEndPos={SetEmissionSP100Config.Instance.nEndPos}, dMeanThreshold={SetEmissionSP100Config.Instance.dMeanThreshold}");
                    int ret = Spectrometer.CM_SetEmissionSP100(SpectrometerHandle, SetEmissionSP100Config.Instance.IsEnabled, SetEmissionSP100Config.Instance.nStartPos, SetEmissionSP100Config.Instance.nEndPos, SetEmissionSP100Config.Instance.dMeanThreshold);
                    if (ret != 1)
                        log.Warn($"SP100 参数设置失败: {Spectrometer.GetErrorMessage(ret)}");

                    State2.Text = Spectrum.Properties.Resources.连接成功;
                    State4.Text = "SP-100";
                    button3.IsEnabled = true;
                    button5.IsEnabled = true;
                    button6.IsEnabled = true;
                }
                else
                {
                    Manager.IsConnected = false;
                    string errorMsg = Spectrometer.GetErrorMessage(iR);
                    log.Error($"光谱仪连接失败: {errorMsg}");
                    MessageBox.Show(Application.Current.GetActiveWindow(), $"连接失败: {errorMsg}");
                }
            }
            catch(Exception ex)
            {
                log.Error("光谱仪连接异常", ex);
                MessageBox.Show(ex.Message);
            }

        }

        //断开连接
        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            testid = 0;
            IsRun = false;
            ret = Manager.Disconnect();
            Manager.SerialNumber = string.Empty;
            State2.Text = Spectrum.Properties.Resources.未连接;
            State4.Text = "---";
        }

        public async Task ReConnet()
        {
            for (int i = 0; i < 6; i++)
            {
                log.Warn($"尝试重连光谱仪 ({i + 1}/6)");
                int ret = Spectrometer.CM_Emission_Close(Manager.Handle);
                log.Debug($"CM_Emission_Close: {ret}");
                ret = Spectrometer.CM_ReleaseEmission(Manager.Handle);
                log.Debug($"CM_ReleaseEmission: {ret}");
                await Task.Delay(200);
                Manager.Handle = Spectrometer.CM_CreateEmission(0, MyCallback);
                int ncom = 0;
                if (Manager.Config.IsComPort)
                {
                     ncom = int.Parse(Manager.Config.SzComName.Replace("COM", ""));
                }
                int iR = Spectrometer.CM_Emission_Init(SpectrometerHandle, ncom, Manager.Config.BaudRate);
                if (iR == 1)
                {
                    Manager.IsConnected = true;
                    iR = Spectrometer.CM_Emission_LoadWavaLengthFile(SpectrometerHandle, Manager.WavelengthFile);
                    if (iR != 1) log.Warn($"重连后加载波长文件失败: {Spectrometer.GetErrorMessage(iR)}");
                    iR = Spectrometer.CM_Emission_LoadMagiudeFile(SpectrometerHandle, Manager.MaguideFile);
                    if (iR != 1) log.Warn($"重连后加载幅值文件失败: {Spectrometer.GetErrorMessage(iR)}");

                    log.Debug($"重连后设置 SP100: IsEnabled={SetEmissionSP100Config.Instance.IsEnabled}, nStartPos={SetEmissionSP100Config.Instance.nStartPos}, nEndPos={SetEmissionSP100Config.Instance.nEndPos}");
                    ret = Spectrometer.CM_SetEmissionSP100(SpectrometerHandle, SetEmissionSP100Config.Instance.IsEnabled, SetEmissionSP100Config.Instance.nStartPos, SetEmissionSP100Config.Instance.nEndPos, SetEmissionSP100Config.Instance.dMeanThreshold);
                    if (ret != 1) log.Warn($"重连后 SP100 设置失败: {Spectrometer.GetErrorMessage(ret)}");

                    log.Info("光谱仪重连成功");
                    break;
                }
                else
                {
                    log.Debug($"重连尝试 {i + 1} 失败: {Spectrometer.GetErrorMessage(iR)}");
                }
                await Task.Delay(200);
            }


            IsRun = false;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            log.Debug($"设置 SP100 参数: IsEnabled={SetEmissionSP100Config.Instance.IsEnabled}, nStartPos={SetEmissionSP100Config.Instance.nStartPos}, nEndPos={SetEmissionSP100Config.Instance.nEndPos}, dMeanThreshold={SetEmissionSP100Config.Instance.dMeanThreshold}");
            int ret = Spectrometer.CM_SetEmissionSP100(SpectrometerHandle, SetEmissionSP100Config.Instance.IsEnabled, SetEmissionSP100Config.Instance.nStartPos, SetEmissionSP100Config.Instance.nEndPos, SetEmissionSP100Config.Instance.dMeanThreshold);
            if (ret == 1)
            {
                log.Info("SP100 参数设置成功");
                MessageBox.Show("SP100 设置成功");
            }
            else
            {
                string errorMsg = Spectrometer.GetErrorMessage(ret);
                log.Error($"SP100 参数设置失败: {errorMsg}");
                MessageBox.Show($"SP100 设置失败: {errorMsg}");
            }
        }
    }
}
