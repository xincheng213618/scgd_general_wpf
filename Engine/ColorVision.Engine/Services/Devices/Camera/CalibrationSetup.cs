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
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsTest
{
    public partial class CalibrationSetup : Form
    {
        public IntPtr m_hHandle = IntPtr.Zero;
        public List<CalibrationItem> calibrationLibCfg;
        public ProjectSysCfg projectSysCfg = null;
        public string m_szcfgname;

        public CalibrationSetup(IntPtr hHandle, string cfgname)
        {
            InitializeComponent();
            m_szcfgname = cfgname;
            m_hHandle = hHandle;

            projectSysCfg = CommonUtil.LoadCfgFile<ProjectSysCfg>(m_szcfgname);
            if (projectSysCfg == null)
                initsyscfg();
            calibrationLibCfg = projectSysCfg.calibrationLibCfg;
            if (calibrationLibCfg == null)
                calibrationLibCfg = new List<CalibrationItem>();
        }

        private void CameraSetup_Load(object sender, EventArgs e)
        {
            projectSysCfg = CommonUtil.LoadCfgFile<ProjectSysCfg>(m_szcfgname);
            if (projectSysCfg == null)
                initsyscfg();
            calibrationLibCfg = projectSysCfg.calibrationLibCfg;
            if (calibrationLibCfg == null)
                calibrationLibCfg = new List<CalibrationItem>();

            ReflashPointsData();
        }

        private void btn_def_Click(object sender, EventArgs e)
        {
            String cfgText = cvCameraCSLib.GetCfgToJson(m_hHandle, ConfigType.Cfg_Calibration, true);
            calibrationLibCfg = JsonConvert.DeserializeObject<List<CalibrationItem>>(cfgText);
            if (calibrationLibCfg == null)
            {
                calibrationLibCfg = new List<CalibrationItem>();
            }
            ReflashPointsData();
        }

        private void btn_cur_Click(object sender, EventArgs e)
        {
            String cfgText = cvCameraCSLib.GetCfgToJson(m_hHandle, ConfigType.Cfg_Calibration, false);
            calibrationLibCfg = JsonConvert.DeserializeObject<List<CalibrationItem>>(cfgText);
            if(calibrationLibCfg == null)
            {
                calibrationLibCfg =  new List<CalibrationItem>();
            }
            ReflashPointsData();
        }

        private void btn_apply_Click(object sender, EventArgs e)
        {
            Dictionary<string, List<CalibrationItem>> mapExpTimeCfg;
            mapExpTimeCfg = new Dictionary<string, List<CalibrationItem>>();
            mapExpTimeCfg.Add("channelCfg", new List<CalibrationItem>());
            mapExpTimeCfg["channelCfg"] = calibrationLibCfg;

            string szJson = Newtonsoft.Json.JsonConvert.SerializeObject(mapExpTimeCfg);
            cvCameraCSLib.UpdateCfgJson(m_hHandle, ConfigType.Cfg_ExpTime, szJson);

            projectSysCfg = CommonUtil.LoadCfgFile<ProjectSysCfg>(m_szcfgname);
            if (projectSysCfg == null)
                initsyscfg();
            projectSysCfg.calibrationLibCfg = calibrationLibCfg;
            CommonUtil.SaveCfgFile<ProjectSysCfg>(m_szcfgname, projectSysCfg);
        }

        private void initsyscfg()
        {
            projectSysCfg = new ProjectSysCfg();

            string strText;
            //strText = cvCameraCSLib.GetDefaultCameraCfgToJson(m_hHandle, true);
            //projectSysCfg.cameraCfg = JsonConvert.DeserializeObject<CameraCfg>(strText);

            //strText = cvCameraCSLib.GetDefaultExpTimeCfgToJson(m_hHandle, true);
            //projectSysCfg.expTimeCfg = JsonConvert.DeserializeObject<ExpTimeCfg>(strText);

            //strText = cvCameraCSLib.GetDefaultChannelsCfgToJson(m_hHandle, true);
            //projectSysCfg.channelCfg = JsonConvert.DeserializeObject<List<ChannelCfg>>(strText);

            strText = cvCameraCSLib.GetCfgToJson(m_hHandle, ConfigType.Cfg_Calibration, true);
            projectSysCfg.calibrationLibCfg = JsonConvert.DeserializeObject<List<CalibrationItem>>(strText);
        }

        private void ReflashPointsData()
        {
            dataGridView1.DataSource = calibrationLibCfg;
            dataGridView1.DataSource = new BindingSource(new BindingList<CalibrationItem>(calibrationLibCfg), null);
        }
    }
}
