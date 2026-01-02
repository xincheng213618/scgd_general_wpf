using cvColorVision;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace WindowsFormsTest
{
    [JsonObject(MemberSerialization.OptIn)]
    public partial class FormCfg : Form
    {
        [JsonProperty]
        public bool m_bV1 = false;

        public int m_nChannelCount = 1; 
        public IntPtr m_hHandle = IntPtr.Zero;
        public ProjectSysCfg projectSysCfg = null;
        public FormChannels formChannels = null;
        public FormCalibCfg calibCfg = new FormCalibCfg();
        public CalibV1 calibV = new CalibV1();

        public string strLoadname = "cfg//FormCfg.cfg";
        public string strPathSysCfg = "cfg\\sys.cfg";

        public FormCfg(IntPtr hHandle, string cfgname)
        {
            if (File.Exists(strLoadname))
            {
                string json = System.IO.File.ReadAllText(strLoadname);
                JsonConvert.PopulateObject(json, this);
            }

            InitializeComponent();
            m_hHandle = hHandle;
            projectSysCfg = CommonUtil.LoadCfgFile<ProjectSysCfg>(cfgname); 
            formChannels = new FormChannels(cfgname);

            if (projectSysCfg == null)
            {
                initsyscfg();
            }
        }

        private void FormCfg_Load(object sender, EventArgs e)
        {
            radioBtn_old.Checked = !m_bV1;
            radioBtn_V1.Checked = m_bV1;
            projectSysCfg = null;

            if (projectSysCfg == null)
            {
                string strText = cvCameraCSLib.GetCfgToJson(m_hHandle, ConfigType.Cfg_SYSTEM, false);
                projectSysCfg = CommonUtil.LoadCfgText<ProjectSysCfg>(strText);
            }

            if(projectSysCfg.cameraCfg == null)
            {
                string strText = cvCameraCSLib.GetCfgToJson(m_hHandle, ConfigType.Cfg_Camera, false);
                projectSysCfg.cameraCfg = CommonUtil.LoadCfgText<CameraCfg>(strText);
            }

            if (projectSysCfg.expTimeCfg == null)
            {
                string strText = cvCameraCSLib.GetCfgToJson(m_hHandle, ConfigType.Cfg_ExpTime, false);
                projectSysCfg.expTimeCfg = CommonUtil.LoadCfgText<ExpTimeCfg>(strText);
            }

            if (projectSysCfg.channelCfg == null)
            {
                string strText = cvCameraCSLib.GetCfgToJson(m_hHandle, ConfigType.Cfg_Channels, false);
                projectSysCfg.channelCfg = CommonUtil.LoadCfgText<List<ChannelCfg>>(strText);
            }

            if (projectSysCfg.calibrationLibCfg == null)
            {
                string strText = cvCameraCSLib.GetCfgToJson(m_hHandle, ConfigType.Cfg_Calibration, false);
                projectSysCfg.calibrationLibCfg = CommonUtil.LoadCfgText<List<CalibrationItem>>(strText);
            }

            proGridcamera.SelectedObject = projectSysCfg.cameraCfg;
            proGridexpTime.SelectedObject = projectSysCfg.expTimeCfg;
        }

        private void FormCfg_FormClosed(object sender, FormClosedEventArgs e)
        {
            CommonUtil.SaveCfgFile<FormCfg>(strLoadname, this);
        }

        private string ConvertFormatJsonString(string str)
        {
            //格式化json字符串
            JsonSerializer serializer = new JsonSerializer();
            TextReader tr = new StringReader(str);
            JsonTextReader jtr = new JsonTextReader(tr);
            object obj = serializer.Deserialize(jtr);
            if (obj != null)
            {
                StringWriter textWriter = new StringWriter();
                JsonTextWriter jsonWriter = new JsonTextWriter(textWriter)
                {
                    Formatting = Formatting.Indented,
                    Indentation = 4,
                    IndentChar = ' '
                };
                serializer.Serialize(jsonWriter, obj);
                return textWriter.ToString();
            }
            else
            {
                return str;
            }
        }

        private void initsyscfg()
        {
            string json = cvCameraCSLib.GetCfgToJson(m_hHandle, ConfigType.Cfg_SYSTEM, true);
            projectSysCfg = CommonUtil.LoadCfgText<ProjectSysCfg>(json);

            if(projectSysCfg == null)
            {
                projectSysCfg = new ProjectSysCfg();
            }

            if (projectSysCfg.cameraCfg == null)
            {
                string strText = cvCameraCSLib.GetCfgToJson(m_hHandle, ConfigType.Cfg_Camera, true);
                projectSysCfg.cameraCfg = CommonUtil.LoadCfgText<CameraCfg>(strText);

                if (projectSysCfg.cameraCfg == null)
                {
                    projectSysCfg.cameraCfg = new CameraCfg();
                }
            }

            if (projectSysCfg.expTimeCfg == null)
            {
                string strText = cvCameraCSLib.GetCfgToJson(m_hHandle, ConfigType.Cfg_ExpTime, true);
                projectSysCfg.expTimeCfg = CommonUtil.LoadCfgText<ExpTimeCfg>(strText);

                if (projectSysCfg.expTimeCfg == null)
                {
                    projectSysCfg.expTimeCfg = new ExpTimeCfg();
                }
            }

            if (projectSysCfg.channelCfg == null)
            {
                string strText = cvCameraCSLib.GetCfgToJson(m_hHandle, ConfigType.Cfg_Channels, true);
                projectSysCfg.channelCfg = CommonUtil.LoadCfgText<List<ChannelCfg>>(strText);

                if (projectSysCfg.channelCfg == null)
                {
                    projectSysCfg.channelCfg = new List<ChannelCfg>();
                }
            }

            if (projectSysCfg.calibrationLibCfg == null)
            {
                string strText = cvCameraCSLib.GetCfgToJson(m_hHandle, ConfigType.Cfg_Calibration, true);
                projectSysCfg.calibrationLibCfg = CommonUtil.LoadCfgText<List<CalibrationItem>>(strText);

                if (projectSysCfg.calibrationLibCfg == null)
                {
                    projectSysCfg.calibrationLibCfg = new List<CalibrationItem>();
                }
            }

            this.richTextBox1.Text = ConvertFormatJsonString(JsonConvert.SerializeObject(projectSysCfg));
        }

        private void btn_Save_Click(object sender, EventArgs e)
        {
            MessageBox.Show("保存成功");
        }

        private void btn_Initial_Click(object sender, EventArgs e)
        {
            initsyscfg();

            proGridcamera.SelectedObject = projectSysCfg.cameraCfg;
            proGridexpTime.SelectedObject = projectSysCfg.expTimeCfg;
        }

        private void btn_Generate_Click(object sender, EventArgs e)
        {
            this.richTextBox1.Text = ConvertFormatJsonString(JsonConvert.SerializeObject(projectSysCfg));
        }

        private void button1_Click(object sender, EventArgs e)
        {
            List<string> listCali = new List<string>();
            if(projectSysCfg.calibrationLibCfg != null)
            for (int i = 0; i < projectSysCfg.calibrationLibCfg.Count; i++)
            {
                listCali.Add(projectSysCfg.calibrationLibCfg[i].title);
            }

            calibCfg.listCali = listCali;

            calibCfg.m_nMode = m_nChannelCount;
            calibCfg.ShowDialog();

            //int nUniformIndex = 0;
            //int nDistortIndex = 0;

            //for (int i = 0; i < projectSysCfg.calibrationLibCfg.Count; i++)
            //{
            //    CalibrationItem item = projectSysCfg.calibrationLibCfg[i];

            //    if (item.type == CalibrationType.DarkNoise)
            //    {
            //        calibCfg.strDarkNoiseCali = item.doc;
            //    }

            //    if (item.type == CalibrationType.DSNU)
            //    {
            //        calibCfg.strDSNUCali = item.doc;
            //    }

            //    if (item.type == CalibrationType.DefectPoint)
            //    {
            //        calibCfg.strDefectCali = item.doc;
            //    }

            //    if (item.type == CalibrationType.Uniformity)
            //    {
            //        calibCfg.szUniformCali[nUniformIndex++] = item.doc;
            //    }

            //    if (item.type == CalibrationType.Distortion)
            //    {
            //        calibCfg.szDistortCali[nDistortIndex++] = item.doc;
            //    }

            //    if (item.type == CalibrationType.Luminance)
            //    {
            //        calibCfg.strBrightCali = item.doc;
            //    }

            //    if (item.type == CalibrationType.LumOneColor)
            //    {
            //        calibCfg.strOneCali = item.doc;
            //    }

            //    if (item.type == CalibrationType.LumFourColor)
            //    {
            //        calibCfg.strFourColorCali = item.doc;
            //    }

            //    if (item.type == CalibrationType.LumMultiColor)
            //    {
            //        calibCfg.strMulColorCali = item.doc;
            //    }
            //}

            //if (calibCfg.ShowDialog() == DialogResult.OK)
            //{
            //    projectSysCfg.calibrationLibCfg.Clear();

            //    CalibrationItem item;

            //    if (calibCfg.strDarkNoiseCali != "")
            //    {
            //        item = new CalibrationItem();

            //        item.title = calibCfg.strDarkNoiseCali;
            //        item.doc = calibCfg.strDarkNoiseCali;
            //        item.type = CalibrationType.DarkNoise;
            //        item.enable = false;

            //        projectSysCfg.calibrationLibCfg.Add(item);
            //    }

            //    if (calibCfg.strDSNUCali != "")
            //    {
            //        item = new CalibrationItem();

            //        item.title = calibCfg.strDSNUCali;
            //        item.doc = calibCfg.strDSNUCali;
            //        item.type = CalibrationType.DSNU;
            //        item.enable = false;

            //        projectSysCfg.calibrationLibCfg.Add(item);
            //    }

            //    if (calibCfg.strDefectCali != "")
            //    {
            //        item = new CalibrationItem();

            //        item.title = calibCfg.strDefectCali;
            //        item.doc = calibCfg.strDefectCali;
            //        item.type = CalibrationType.DefectPoint;
            //        item.enable = false;

            //        projectSysCfg.calibrationLibCfg.Add(item);
            //    }

            //    for (int i = 0; i < m_nChannelCount; i++)
            //    {
            //        if (calibCfg.szUniformCali[i] != "")
            //        {
            //            item = new CalibrationItem();

            //            item.title = calibCfg.szUniformCali[i];
            //            item.doc = calibCfg.szUniformCali[i];
            //            item.type = CalibrationType.Uniformity;
            //            item.enable = false;

            //            projectSysCfg.calibrationLibCfg.Add(item);
            //        }

            //        if (calibCfg.szDistortCali[i] != "")
            //        {
            //            item = new CalibrationItem();

            //            item.title = calibCfg.szDistortCali[i];
            //            item.doc = calibCfg.szDistortCali[i];
            //            item.type = CalibrationType.Distortion;
            //            item.enable = false;
            //            projectSysCfg.calibrationLibCfg.Add(item);
            //        }
            //    }

            //    if (calibCfg.strBrightCali != "")
            //    {
            //        item = new CalibrationItem();

            //        item.title = calibCfg.strBrightCali;
            //        item.doc = calibCfg.strBrightCali;
            //        item.type = CalibrationType.Luminance;
            //        item.enable = false;

            //        projectSysCfg.calibrationLibCfg.Add(item);
            //    }

            //    if (calibCfg.strOneCali != "")
            //    {
            //        item = new CalibrationItem();

            //        item.title = calibCfg.strOneCali;
            //        item.doc = calibCfg.strOneCali;
            //        item.type = CalibrationType.LumOneColor;
            //        item.enable = false;

            //        projectSysCfg.calibrationLibCfg.Add(item);
            //    }

            //    if (calibCfg.strFourColorCali != "")
            //    {
            //        item = new CalibrationItem();

            //        item.title = calibCfg.strFourColorCali;
            //        item.doc = calibCfg.strFourColorCali;
            //        item.type = CalibrationType.LumFourColor;
            //        item.enable = false;

            //        projectSysCfg.calibrationLibCfg.Add(item);
            //    }

            //    if (calibCfg.strMulColorCali != "")
            //    {
            //        item = new CalibrationItem();

            //        item.title = calibCfg.strMulColorCali;
            //        item.doc = calibCfg.strMulColorCali;
            //        item.type = CalibrationType.LumMultiColor;
            //        item.enable = false;

            //        projectSysCfg.calibrationLibCfg.Add(item);
            //    }
            //}
        }

        private void button2_Click(object sender, EventArgs e)
        {
            formChannels.m_nMode = m_nChannelCount;

            for (int i = 0; i < projectSysCfg.channelCfg.Count; i++)
            {
                if(i >= 3)
                {
                    break;
                }

                formChannels.channelCfg[i] = projectSysCfg.channelCfg[i];
            }

            if (formChannels.ShowDialog() == DialogResult.OK)
            {
                projectSysCfg.channelCfg.Clear();

                for (int i = 0; i < m_nChannelCount; i++)
                {
                    projectSysCfg.channelCfg.Add(formChannels.channelCfg[i]);
                }
            }
        }

        private void btn_apply_Click(object sender, EventArgs e)
        {
            //             Dictionary<string, List<CalibrationItem>> mapCalibration;
            // 
            //             mapCalibration = new Dictionary<string, List<CalibrationItem>>();
            //             mapCalibration.Add("calibrationLibCfg", new List<CalibrationItem>());
            // 
            //             List<CalibrationItem> list = mapCalibration["calibrationLibCfg"];
            // 
            //             foreach (var item in projectSysCfg.calibrationLibCfg)
            //             {
            //                 list.Add(item);
            //             }
            // 
            //             string szJson = Newtonsoft.Json.JsonConvert.SerializeObject(mapCalibration);
            //             cvCameraCSLib.UpdateCfgJson(m_hHandle, ConfigType.Cfg_SYSTEM, szJson);

            string szJson = ConvertFormatJsonString(JsonConvert.SerializeObject(projectSysCfg));

            cvCameraCSLib.UpdateCfgJson(m_hHandle, ConfigType.Cfg_SYSTEM, szJson);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            CalibrationSysSetup sysSetup = new CalibrationSysSetup(projectSysCfg.calibrationLibCfg);

            if (sysSetup.ShowDialog() == DialogResult.OK)
            {
                projectSysCfg.calibrationLibCfg = sysSetup.listItem;
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            calibV.listSYS = projectSysCfg.calibrationLibCfg;
            calibV.ShowDialog();
        }

        private void radioBtn_old_CheckedChanged(object sender, EventArgs e)
        {
            m_bV1 = !radioBtn_old.Checked;
        }

        private void radioBtn_V1_CheckedChanged(object sender, EventArgs e)
        {
            m_bV1 = radioBtn_V1.Checked;
        }

    }
}
