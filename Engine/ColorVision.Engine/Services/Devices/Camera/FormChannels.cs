using cvColorVision;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsTest
{
    public partial class FormChannels : Form
    {
        public int m_nMode;
        public ChannelCfg[] channelCfg = new ChannelCfg[3];
        string[] szTypeText = new string[3];
        public string m_szcfgname;

        public FormChannels(string cfgname)
        {
            InitializeComponent();

            m_szcfgname = cfgname;

            szTypeText[0] = "R(红)";
            szTypeText[1] = "G(绿)";
            szTypeText[2] = "B(蓝)";

            channelCfg[0] = new ChannelCfg();
            channelCfg[1] = new ChannelCfg();
            channelCfg[2] = new ChannelCfg();
        }

        private void FormChannels_Load(object sender, EventArgs e)
        {
            if (m_nMode == 1)
            {
                gb_Channel1.Visible = true;
                gb_Channel2.Visible = false;
                gb_Channel3.Visible = false;
                tb_Channel1Title.Text = channelCfg[0].title;
                tb_Channel1cfwPort.Text = channelCfg[0].cfwport.ToString();
                comboBox1.SelectedIndex = (int)channelCfg[0].chtype;
            }
            else if (m_nMode == 3)
            {
                gb_Channel1.Visible = true;
                gb_Channel2.Visible = true;
                gb_Channel3.Visible = true;
                tb_Channel1Title.Text = channelCfg[0].title;
                tb_Channel1cfwPort.Text = channelCfg[0].cfwport.ToString();
                comboBox1.SelectedIndex = (int)channelCfg[0].chtype;

                tb_Channel2Title.Text = channelCfg[1].title;
                tb_Channel2cfwPort.Text = channelCfg[1].cfwport.ToString();
                comboBox2.SelectedIndex = (int)channelCfg[1].chtype;

                tb_Channel3Title.Text = channelCfg[2].title;
                tb_Channel3cfwPort.Text = channelCfg[2].cfwport.ToString();
                comboBox3.SelectedIndex = (int)channelCfg[2].chtype;
            }
        }

        private void btn_Confirm_Click(object sender, EventArgs e)
        {
            if (m_nMode == 1)
            {
                channelCfg[0].title = tb_Channel1Title.Text;
                channelCfg[0].cfwport = ushort.Parse(tb_Channel1cfwPort.Text);
                channelCfg[0].chtype = (ImageChannelType)(int.Parse(tb_Channel1cfwPort.Text));
            }
            else if(m_nMode == 3)
            {
                int cht0 = comboBox1.SelectedIndex;
                int cht1 = comboBox2.SelectedIndex;
                int cht2 = comboBox3.SelectedIndex;

                if (cht0 == cht1 ||
                    cht1 == cht2 ||
                    cht0 == cht2)
                {
                    MessageBox.Show("三通道中的chtype不能相同！");

                    return;
                }

                channelCfg[0].title = tb_Channel1Title.Text;
                channelCfg[0].cfwport = ushort.Parse(tb_Channel1cfwPort.Text);
                channelCfg[0].chtype = (ImageChannelType)(comboBox1.SelectedIndex);

                channelCfg[1].title = tb_Channel2Title.Text;
                channelCfg[1].cfwport = ushort.Parse(tb_Channel2cfwPort.Text);
                channelCfg[1].chtype = (ImageChannelType)(comboBox2.SelectedIndex);

                channelCfg[2].title = tb_Channel3Title.Text;
                channelCfg[2].cfwport = ushort.Parse(tb_Channel3cfwPort.Text);
                channelCfg[2].chtype = (ImageChannelType)(comboBox3.SelectedIndex);
            }

            this.DialogResult = DialogResult.OK;
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex >= 3 || comboBox1.SelectedIndex < 0)
                return;
            tb_Channel1Title.Text = szTypeText[comboBox1.SelectedIndex];
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox2.SelectedIndex >= 3 || comboBox2.SelectedIndex < 0)
                return;
            tb_Channel2Title.Text = szTypeText[comboBox2.SelectedIndex];
        }

        private void comboBox3_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox3.SelectedIndex >= 3 || comboBox3.SelectedIndex < 0)
                return;
            tb_Channel3Title.Text = szTypeText[comboBox3.SelectedIndex];
        }

        private int LoopIndex(int nIndex)
        {
            if(nIndex < 0)
            {
                nIndex = 2;
            }

            if(nIndex > 2)
            {
                nIndex = 0;
            }

            return nIndex;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
        }
    }
}
