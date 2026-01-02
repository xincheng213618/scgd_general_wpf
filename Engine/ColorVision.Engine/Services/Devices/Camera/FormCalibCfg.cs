//using cvColorVision;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace WindowsFormsTest
{
    [JsonObject(MemberSerialization.OptIn)]

    public partial class FormCalibCfg : Form
    {
        public string syscfgJson;
        public int m_nMode;

        [JsonProperty]
        public string[] szDistortCali = new string[3] { "", "", "" };
        [JsonProperty]
        public string[] szUniformCali = new string[3] { "", "", "" };
        [JsonProperty]
        public string[] szDSNUCali = new string[3] { "", "", "" };
        [JsonProperty]
        public string strDefectCali = "";
        [JsonProperty]
        public string strDarkNoiseCali = "";
        [JsonProperty]
        public string strOneCali = "";
        [JsonProperty]
        public string strFourColorCali = "";
        [JsonProperty]
        public string strMulColorCali = "";
        [JsonProperty]
        public string strBrightCali = "";

        public List<string> listCali;

        public string cfgFile = "cfg//CalibCfg.cfg";

        public FormCalibCfg()
        {
            if (File.Exists(cfgFile))
            {
                string json = System.IO.File.ReadAllText(cfgFile);
                JsonConvert.PopulateObject(json, this);
            }

            InitializeComponent();
        }

        private void FormCalibCfg_Load(object sender, EventArgs e)
        {
            if (m_nMode == 1)
            {
                if (tbc_Calib.TabPages.Contains(tbp_0) == false)
                {
                    tbc_Calib.TabPages.Add(tbp_0);
                }

                if (tbc_Calib.TabPages.Contains(tbp_1))
                {
                    tbc_Calib.TabPages.Remove(tbp_1);
                }

                if (tbc_Calib.TabPages.Contains(tbp_2))
                {
                    tbc_Calib.TabPages.Remove(tbp_2);
                }

                if (tbc_MonoMulti.TabPages.Contains(tp_Mono) == false)
                {
                    tbc_MonoMulti.TabPages.Add(tp_Mono);
                }

                if (tbc_MonoMulti.TabPages.Contains(tp_MultiColor))
                {
                    tbc_MonoMulti.TabPages.Remove(tp_MultiColor);
                }
            }
            else if (m_nMode == 3)
            {
                if (tbc_Calib.TabPages.Contains(tbp_0) == false)
                {
                    tbc_Calib.TabPages.Add(tbp_0);
                }

                if (tbc_Calib.TabPages.Contains(tbp_1) == false)
                {
                    tbc_Calib.TabPages.Add(tbp_1);
                }

                if (tbc_Calib.TabPages.Contains(tbp_2) == false)
                {
                    tbc_Calib.TabPages.Add(tbp_2);
                }

                if (tbc_MonoMulti.TabPages.Contains(tp_Mono))
                {
                    tbc_MonoMulti.TabPages.Remove(tp_Mono);
                }

                if (tbc_MonoMulti.TabPages.Contains(tp_MultiColor) == false)
                {
                    tbc_MonoMulti.TabPages.Add(tp_MultiColor);
                }
            }

            tb_DistortCaliPath.DataSource = new List<string>(listCali);
            tb_DistortCali2Path.DataSource = new List<string>(listCali);
            tb_DistortCali3Path.DataSource = new List<string>(listCali);

            tb_UniformFieldCaliPath.DataSource = new List<string>(listCali);
            tb_UniformFieldCali2Path.DataSource = new List<string>(listCali);
            tb_UniformFieldCali3Path.DataSource = new List<string>(listCali);

            tb_DSNUPath.DataSource = new List<string>(listCali);
            tb_DSNU2Path.DataSource = new List<string>(listCali);
            tb_DSNU3Path.DataSource = new List<string>(listCali);

            tb_DefectPixelCaliBrightPath.DataSource = new List<string>(listCali);
            tb_DefectPixelCaliDarkPath.DataSource = new List<string>(listCali);

            cb_BrightCaliPath.DataSource = new List<string>(listCali);
            cb_MonoCaliPath.DataSource = new List<string>(listCali);
            cb_FourColorCaliPath.DataSource = new List<string>(listCali);
            cb_MulColorCaliPath.DataSource = new List<string>(listCali);

            tb_DistortCaliPath.SelectedIndex = -1;
            tb_DistortCaliPath.SelectedIndex = -1;
            tb_DistortCali2Path.SelectedIndex = -1;
            tb_DistortCali3Path.SelectedIndex = -1;

            tb_UniformFieldCaliPath.SelectedIndex = -1;
            tb_UniformFieldCali2Path.SelectedIndex = -1;
            tb_UniformFieldCali3Path.SelectedIndex = -1;

            tb_DSNUPath.SelectedIndex = -1;
            tb_DSNU2Path.SelectedIndex = -1;
            tb_DSNU3Path.SelectedIndex = -1;

            tb_DefectPixelCaliBrightPath.SelectedIndex = -1;
            tb_DefectPixelCaliDarkPath.SelectedIndex = -1;

            cb_BrightCaliPath.SelectedIndex = -1;
            cb_MonoCaliPath.SelectedIndex = -1;
            cb_FourColorCaliPath.SelectedIndex = -1;
            cb_MulColorCaliPath.SelectedIndex = -1;

            tb_DistortCaliPath.Text = szDistortCali[0];
            tb_DistortCali2Path.Text = szDistortCali[1];
            tb_DistortCali3Path.Text = szDistortCali[2];

            tb_UniformFieldCaliPath.Text = szUniformCali[0];
            tb_UniformFieldCali2Path.Text = szUniformCali[1];
            tb_UniformFieldCali3Path.Text = szUniformCali[2];

            tb_DSNUPath.Text = szDSNUCali[0];
            tb_DSNU2Path.Text = szDSNUCali[1];
            tb_DSNU3Path.Text = szDSNUCali[2];

            tb_DefectPixelCaliBrightPath.Text = strDefectCali;
            tb_DefectPixelCaliDarkPath.Text = strDarkNoiseCali;

            cb_BrightCaliPath.Text = strBrightCali;
            cb_MonoCaliPath.Text = strOneCali;
            cb_FourColorCaliPath.Text = strFourColorCali;
            cb_MulColorCaliPath.Text = strMulColorCali;
        }

        private void btn_DSNU_Click(object sender, EventArgs e)
        {
            string apppath = Environment.CurrentDirectory.ToString();
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = apppath;
            openFileDialog.Filter = "DAT|*.dat||";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string fName;
                fName = openFileDialog.FileName;
                if (!fName.Contains(apppath))
                {
                    MessageBox.Show("请选择软件根目录文件！");
                    return;
                }
                int fileNameLength = fName.LastIndexOf('\\') + 1;
                string fileName = fName.Substring(fileNameLength, fName.Length - fileNameLength);
                //tb_DSNUFileName.Text = fileName.Split('.')[0];
                int pathLength = fName.IndexOf(apppath) + apppath.Length + 1;
                string pathName = fName.Substring(pathLength, fName.Length - pathLength);
                tb_DSNUPath.Text = pathName;
            }
        }

        private void btn_DefectPixelCaliBright_Click(object sender, EventArgs e)
        {
            string apppath = Environment.CurrentDirectory.ToString();
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = apppath;
            openFileDialog.Filter = "DAT|*.dat||";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string fName;
                fName = openFileDialog.FileName;
                if (!fName.Contains(apppath))
                {
                    MessageBox.Show("请选择软件根目录文件！");
                    return;
                }
                int fileNameLength = fName.LastIndexOf('\\') + 1;
                string fileName = fName.Substring(fileNameLength, fName.Length - fileNameLength);
                //tb_DefectPixelCaliBrightFileName.Text = fileName.Split('.')[0];
                int pathLength = fName.IndexOf(apppath) + apppath.Length + 1;
                string pathName = fName.Substring(pathLength, fName.Length - pathLength);
                tb_DefectPixelCaliBrightPath.Text = pathName;
            }
        }

        private void btn_DefectPixelCaliDark_Click(object sender, EventArgs e)
        {
            string apppath = Environment.CurrentDirectory.ToString();
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = apppath;
            openFileDialog.Filter = "DAT|*.dat||";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string fName;
                fName = openFileDialog.FileName;
                if (!fName.Contains(apppath))
                {
                    MessageBox.Show("请选择软件根目录文件！");
                    return;
                }
                int fileNameLength = fName.LastIndexOf('\\') + 1;
                string fileName = fName.Substring(fileNameLength, fName.Length - fileNameLength);
                //tb_DarkNoiselFileName.Text = fileName.Split('.')[0];
                int pathLength = fName.IndexOf(apppath) + apppath.Length + 1;
                string pathName = fName.Substring(pathLength, fName.Length - pathLength);
                tb_DefectPixelCaliDarkPath.Text = pathName;
            }
        }

        private void btn_DistortCali_Click(object sender, EventArgs e)
        {
            string apppath =  Environment.CurrentDirectory.ToString();
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = apppath;
            openFileDialog.Filter = "DAT|*.dat||";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string fName;
                fName = openFileDialog.FileName;
                if(!fName.Contains(apppath))
                {
                    MessageBox.Show("请选择软件根目录文件！");
                    return;
                }
                int fileNameLength = fName.LastIndexOf('\\') + 1;
                string fileName = fName.Substring(fileNameLength, fName.Length - fileNameLength);
                //tb_DistortCaliFileName.Text = fileName.Split('.')[0];
                int pathLength = fName.IndexOf(apppath) + apppath.Length + 1;
                string pathName = fName.Substring(pathLength, fName.Length - pathLength);
                tb_DistortCaliPath.Text = pathName;
            }
        }

        private void btn_UniformFieldCali_Click(object sender, EventArgs e)
        {
            string apppath = Environment.CurrentDirectory.ToString();
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = apppath;
            openFileDialog.Filter = "DAT|*.dat||";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string fName;
                fName = openFileDialog.FileName;
                if (!fName.Contains(apppath))
                {
                    MessageBox.Show("请选择软件根目录文件！");
                    return;
                }
                int fileNameLength = fName.LastIndexOf('\\') + 1;
                string fileName = fName.Substring(fileNameLength, fName.Length - fileNameLength);
                //tb_UniformFieldCaliFileName.Text = fileName.Split('.')[0];
                int pathLength = fName.IndexOf(apppath) + apppath.Length + 1;
                string pathName = fName.Substring(pathLength, fName.Length - pathLength);
                tb_UniformFieldCaliPath.Text = pathName;
            }
        }

        private void btn_BrightCali_Click(object sender, EventArgs e)
        {
            string apppath = Environment.CurrentDirectory.ToString();
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = apppath;
            openFileDialog.Filter = "DAT|*.dat||";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string fName;
                fName = openFileDialog.FileName;
                if (!fName.Contains(apppath))
                {
                    MessageBox.Show("请选择软件根目录文件！");
                    return;
                }
                int fileNameLength = fName.LastIndexOf('\\') + 1;
                string fileName = fName.Substring(fileNameLength, fName.Length - fileNameLength);
                //cb_BrightCaliFileName.Text = fileName.Split('.')[0];
                int pathLength = fName.IndexOf(apppath) + apppath.Length + 1;
                string pathName = fName.Substring(pathLength, fName.Length - pathLength);
                cb_BrightCaliPath.Text = pathName;
            }
        }

        private void btn_MonoCali_Click(object sender, EventArgs e)
        {
            string apppath = Environment.CurrentDirectory.ToString();
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = apppath;
            openFileDialog.Filter = "DAT|*.dat||";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string fName;
                fName = openFileDialog.FileName;
                if (!fName.Contains(apppath))
                {
                    MessageBox.Show("请选择软件根目录文件！");
                    return;
                }
                int fileNameLength = fName.LastIndexOf('\\') + 1;
                string fileName = fName.Substring(fileNameLength, fName.Length - fileNameLength);
                //cb_MonoCaliFileName.Text = fileName.Split('.')[0];
                int pathLength = fName.IndexOf(apppath) + apppath.Length + 1;
                string pathName = fName.Substring(pathLength, fName.Length - pathLength);
                cb_MonoCaliPath.Text = pathName;
            }
        }

        private void btn_FourColorCali_Click(object sender, EventArgs e)
        {
            string apppath = Environment.CurrentDirectory.ToString();
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = apppath;
            openFileDialog.Filter = "DAT|*.dat||";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string fName;
                fName = openFileDialog.FileName;
                if (!fName.Contains(apppath))
                {
                    MessageBox.Show("请选择软件根目录文件！");
                    return;
                }
                int fileNameLength = fName.LastIndexOf('\\') + 1;
                string fileName = fName.Substring(fileNameLength, fName.Length - fileNameLength);
                //cb_FourColorCaliFileName.Text = fileName.Split('.')[0];
                int pathLength = fName.IndexOf(apppath) + apppath.Length + 1;
                string pathName = fName.Substring(pathLength, fName.Length - pathLength);
                cb_FourColorCaliPath.Text = pathName;
            }
        }

        private void btn_MulColorCali_Click(object sender, EventArgs e)
        {
            string apppath = Environment.CurrentDirectory.ToString();
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = apppath;
            openFileDialog.Filter = "DAT|*.dat||";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string fName;
                fName = openFileDialog.FileName;
                if (!fName.Contains(apppath))
                {
                    MessageBox.Show("请选择软件根目录文件！");
                    return;
                }
                int fileNameLength = fName.LastIndexOf('\\') + 1;
                string fileName = fName.Substring(fileNameLength, fName.Length - fileNameLength);
                //cb_MulColorCaliFileName.Text = fileName.Split('.')[0];
                int pathLength = fName.IndexOf(apppath) + apppath.Length + 1;
                string pathName = fName.Substring(pathLength, fName.Length - pathLength);
                cb_MulColorCaliPath.Text = pathName;
            }
        }

        private void btn_Cancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
        }

        private void btn_Confirm_Click(object sender, EventArgs e)
        {
            szDistortCali[0] = tb_DistortCaliPath.Text;
            szDistortCali[1] = tb_DistortCali2Path.Text;
            szDistortCali[2] = tb_DistortCali3Path.Text;

            szUniformCali[0] = tb_UniformFieldCaliPath.Text;
            szUniformCali[1] = tb_UniformFieldCali2Path.Text;
            szUniformCali[2] = tb_UniformFieldCali3Path.Text;

            szDSNUCali[0] = tb_DSNUPath.Text;
            szDSNUCali[1] = tb_DSNU2Path.Text;
            szDSNUCali[2] = tb_DSNU3Path.Text;

            strDefectCali = tb_DefectPixelCaliBrightPath.Text;
            strDarkNoiseCali = tb_DefectPixelCaliDarkPath.Text;

            strBrightCali = cb_BrightCaliPath.Text;
            strOneCali = cb_MonoCaliPath.Text;
            strFourColorCali = cb_FourColorCaliPath.Text;
            strMulColorCali = cb_MulColorCaliPath.Text;

            CommonUtil.SaveCfgFile<FormCalibCfg>(cfgFile, this);

            this.DialogResult = DialogResult.OK;
        }

        private void btn_DistortCali2_Click(object sender, EventArgs e)
        {
            string apppath = Environment.CurrentDirectory.ToString();
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = apppath;
            openFileDialog.Filter = "DAT|*.dat||";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string fName;
                fName = openFileDialog.FileName;
                if (!fName.Contains(apppath))
                {
                    MessageBox.Show("请选择软件根目录文件！");
                    return;
                }
                int fileNameLength = fName.LastIndexOf('\\') + 1;
                string fileName = fName.Substring(fileNameLength, fName.Length - fileNameLength);
                //tb_DistortCali2FileName.Text = fileName.Split('.')[0];
                int pathLength = fName.IndexOf(apppath) + apppath.Length + 1;
                string pathName = fName.Substring(pathLength, fName.Length - pathLength);
                tb_DistortCali2Path.Text = pathName;
            }
        }

        private void btn_DistortCali3_Click(object sender, EventArgs e)
        {
            string apppath = Environment.CurrentDirectory.ToString();
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = apppath;
            openFileDialog.Filter = "DAT|*.dat||";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string fName;
                fName = openFileDialog.FileName;
                if (!fName.Contains(apppath))
                {
                    MessageBox.Show("请选择软件根目录文件！");
                    return;
                }
                int fileNameLength = fName.LastIndexOf('\\') + 1;
                string fileName = fName.Substring(fileNameLength, fName.Length - fileNameLength);
                //tb_DistortCali3FileName.Text = fileName.Split('.')[0];
                int pathLength = fName.IndexOf(apppath) + apppath.Length + 1;
                string pathName = fName.Substring(pathLength, fName.Length - pathLength);
                tb_DistortCali3Path.Text = pathName;
            }
        }

        private void btn_UniformFieldCali2_Click(object sender, EventArgs e)
        {
            string apppath = Environment.CurrentDirectory.ToString();
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = apppath;
            openFileDialog.Filter = "DAT|*.dat||";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string fName;
                fName = openFileDialog.FileName;
                if (!fName.Contains(apppath))
                {
                    MessageBox.Show("请选择软件根目录文件！");
                    return;
                }
                int fileNameLength = fName.LastIndexOf('\\') + 1;
                string fileName = fName.Substring(fileNameLength, fName.Length - fileNameLength);
                //tb_UniformFieldCali2FileName.Text = fileName.Split('.')[0];
                int pathLength = fName.IndexOf(apppath) + apppath.Length + 1;
                string pathName = fName.Substring(pathLength, fName.Length - pathLength);
                tb_UniformFieldCali2Path.Text = pathName;
            }
        }

        private void btn_UniformFieldCali3_Click(object sender, EventArgs e)
        {
            string apppath = Environment.CurrentDirectory.ToString();
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = apppath;
            openFileDialog.Filter = "DAT|*.dat||";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string fName;
                fName = openFileDialog.FileName;
                if (!fName.Contains(apppath))
                {
                    MessageBox.Show("请选择软件根目录文件！");
                    return;
                }
                int fileNameLength = fName.LastIndexOf('\\') + 1;
                string fileName = fName.Substring(fileNameLength, fName.Length - fileNameLength);
                //tb_UniformFieldCali3FileName.Text = fileName.Split('.')[0];
                int pathLength = fName.IndexOf(apppath) + apppath.Length + 1;
                string pathName = fName.Substring(pathLength, fName.Length - pathLength);
                tb_UniformFieldCali3Path.Text = pathName;
            }
        }

        private void btn_DSNU_Click_1(object sender, EventArgs e)
        {
            string apppath = Environment.CurrentDirectory.ToString();
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = apppath;
            openFileDialog.Filter = "DAT|*.dat||";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string fName;
                fName = openFileDialog.FileName;
                if (!fName.Contains(apppath))
                {
                    MessageBox.Show("请选择软件根目录文件！");
                    return;
                }
                int fileNameLength = fName.LastIndexOf('\\') + 1;
                string fileName = fName.Substring(fileNameLength, fName.Length - fileNameLength);
                //tb_UniformFieldCali3FileName.Text = fileName.Split('.')[0];
                int pathLength = fName.IndexOf(apppath) + apppath.Length + 1;
                string pathName = fName.Substring(pathLength, fName.Length - pathLength);
                tb_DSNUPath.Text = pathName;
            }
        }

        private void btn_DSNU2_Click(object sender, EventArgs e)
        {
            string apppath = Environment.CurrentDirectory.ToString();
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = apppath;
            openFileDialog.Filter = "DAT|*.dat||";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string fName;
                fName = openFileDialog.FileName;
                if (!fName.Contains(apppath))
                {
                    MessageBox.Show("请选择软件根目录文件！");
                    return;
                }
                int fileNameLength = fName.LastIndexOf('\\') + 1;
                string fileName = fName.Substring(fileNameLength, fName.Length - fileNameLength);
                //tb_UniformFieldCali3FileName.Text = fileName.Split('.')[0];
                int pathLength = fName.IndexOf(apppath) + apppath.Length + 1;
                string pathName = fName.Substring(pathLength, fName.Length - pathLength);
                tb_DSNU2Path.Text = pathName;
            }
        }

        private void btn_DSNU3_Click(object sender, EventArgs e)
        {
            string apppath = Environment.CurrentDirectory.ToString();
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = apppath;
            openFileDialog.Filter = "DAT|*.dat||";
            openFileDialog.RestoreDirectory = true;
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string fName;
                fName = openFileDialog.FileName;
                if (!fName.Contains(apppath))
                {
                    MessageBox.Show("请选择软件根目录文件！");
                    return;
                }
                int fileNameLength = fName.LastIndexOf('\\') + 1;
                string fileName = fName.Substring(fileNameLength, fName.Length - fileNameLength);
                int pathLength = fName.IndexOf(apppath) + apppath.Length + 1;
                string pathName = fName.Substring(pathLength, fName.Length - pathLength);
                tb_DSNU3Path.Text = pathName;
            }
        }
    }
}
