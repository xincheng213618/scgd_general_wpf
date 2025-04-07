using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;

namespace CV_Spectrometer
{
    /// <summary>
    /// SpectralCorrection.xaml 的交互逻辑
    /// </summary>
    public partial class SpectralCorrection : Window
    {
        public string FileURL;
        float fIntTime;
        int iAveNum;
        int iFilterBW;
        float fCCT = 2856.0f;
        float fFlux = 100.0f;
        int iIntLimitTime = 6000;
        float fAutoIntTimeB = 0;
        bool autoint = false;

        public SpectralCorrection()
        {
            InitializeComponent();
        }
        public SpectralCorrection(float fInt,int iAve,int iFil,bool auto, int iInt,float fAutoI)
        {
            InitializeComponent();
            fIntTime = fInt;
            iAveNum = iAve;
            iFilterBW = iFil;
            iIntLimitTime = iInt;
            fAutoIntTimeB = fAutoI;
            autoint = auto;

        }
        //读取文件
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog =new Microsoft.Win32.OpenFileDialog();
            dialog.Filter = "校正文件(*.txt)|*.txt|所有文件(*.*)|*.*";
            dialog.InitialDirectory = AppDomain.CurrentDomain.BaseDirectory+ "Calibration";
            if (dialog.ShowDialog() == true)
            {
                textBox.Text = dialog.FileName;
                FileURL= dialog.FileName;
            }
        }
        //校正文件
        private void Button2_Click(object sender, RoutedEventArgs e)
        {
            int iR;
            float fIp=0;
            int iStaNum = 0;
            //float[] fStaWL=new float[10001];
            float[] fStaWL;
            //float[] fStaPL = new float[10001];
            float[] fStaPL;
            int iCalType = 1;
            float[] fDarkData = new float[2048];
            List<string> lines = new List<string>();
            List<string> lines2 = new List<string>();
            //读取校零文件
            String dark = AppDomain.CurrentDomain.BaseDirectory + "Calibration" + "/dark.nh";
            using (StreamReader sr2 = new StreamReader(@dark, Encoding.UTF8, false))
            {
                while (!sr2.EndOfStream)
                {
                    lines2.Add(sr2.ReadLine());
                }
                for (int i = 0; i < 2048; i++)
                {
                    fDarkData[i] = float.Parse(lines2[i]);
                }
            }
            int ret=1;
            if (autoint == true)
            {
                //ret = GCSInterface.JK_GetAutoTime(ref fIntTime, iIntLimitTime, fAutoIntTimeB);
                //if (ret == 0)
                //{
                //}
                //else
                //{
                //    MessageBox.Show("自动积分获取失败");
                //}
            }
            Console.WriteLine(fIntTime);
            Console.WriteLine(iAveNum);
            Console.WriteLine(iFilterBW);
            //光谱法 读取光谱数据 StandrdLamp.txt
            using (StreamReader sr = new StreamReader(@FileURL, Encoding.UTF8, false))
            {
                while (!sr.EndOfStream)
                {
                    lines.Add(sr.ReadLine());
                }
                int start=lines[0].IndexOf("NUM:");
                if (start != -1)
                {
                    lines[0] = lines[0].Remove(start, 4);
                }
                int line0= Convert.ToInt32(lines[0]);
                //MessageBox.Show(line0.ToString());
                //Console.WriteLine(line0);
                iStaNum = line0;
                fStaWL=new float[iStaNum];
                fStaPL = new float[iStaNum];
                for (int i = 1; i < iStaNum+1; i++)
                {
                    string num = lines[i].Substring(0,4);
                    Console.WriteLine(float.Parse(num));
                    fStaWL[i - 1] = float.Parse(num);
                    string num2 = lines[i].Remove(0, 4);
                    fStaPL[i-1]= float.Parse(num2);
                    Console.WriteLine(float.Parse(num2));
                }
            }
            //MessageBox.Show("fIntTime:" + fIntTime.ToString() + ",iAveNum" + iAveNum + ",iFilterBW" + iFilterBW + ",fDarkData" + fDarkData[fDarkData.Length / 2] + ",iCalType" + iCalType + ",fCCT" + fCCT + ",fFlux" + fFlux + ",iStaNum" + iStaNum + ",fStaWL" + fStaWL[fStaWL.Length / 2] + ",fStaPL" + fStaPL[fStaPL.Length / 2]);
            //iR = GCSInterface.JK_Emission_Calib(fIntTime, iAveNum, iFilterBW, fDarkData, iCalType, fCCT, fFlux, iStaNum, fStaWL, fStaPL, ref fIp);
            //iR = GCSInterface.CF_Emission_Calib(fIntTime, iAveNum, iFilterBW, iCalType, fCCT, fFlux, iStaNum, fStaWL, fStaPL, ref fIp);
            //if (iR == 0)
            //{
            //    MessageBox.Show("光谱仪定标成功");
            //}
            //else
            //{
            //    MessageBox.Show("光谱仪定标失败");
            //}
            
            //Console.Read();
        }

        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
