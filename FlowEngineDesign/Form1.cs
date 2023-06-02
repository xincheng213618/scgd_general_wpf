using FlowEngineLib;
using HslCommunication.BasicFramework;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FlowEngineDesign
{
    public partial class Form1 : Form
    {
        private MQTTHelper _MQTTHelper = new MQTTHelper();
        private MQTTHelper _MQTTSever = new MQTTHelper();
        private HslCommunication.BasicFramework.SoftNumericalOrder softNumerical;
        private string svrName;
        public Form1()
        {
            InitializeComponent();
        }
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            stNodePropertyGrid1.Text = "Node_Property";

            //stNodeTreeView1.LoadAssembly(Application.ExecutablePath);
            stNodeTreeView1.LoadAssembly("FlowEngineLib.dll");
            stNodeEditor1.LoadAssembly("FlowEngineLib.dll");

            stNodeEditor1.ActiveChanged += (s, ea) => stNodePropertyGrid1.SetNode(stNodeEditor1.ActiveNode);
            stNodeEditor1.NodeAdded += StNodeEditor1_NodeAdded;
            stNodeEditor1.NodeAdded += (s, ea) => ea.Node.ContextMenuStrip = contextMenuStrip1;

            //stNodePropertyGrid1.SetInfoKey("Author", "Mail", "Link", "Show Help");
            //stNodeTreeView1.PropertyGrid.SetInfoKey("Author", "Mail", "Link", "Show Help");

            stNodeEditor1.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom;

            contextMenuStrip1.ShowImageMargin = false;
            contextMenuStrip1.Renderer = new ToolStripRendererEx();

            softNumerical = new HslCommunication.BasicFramework.SoftNumericalOrder(
                "CV",              // "ABC201711090000001" 中的ABC前缀，代码中仍然可以更改ABC
                "yyyyMMddHH",         // "ABC201711090000001" 中的20171109，可以格式化时间，也可以为""，也可以设置为"yyyyMMddHHmmss";
                5,                  // "ABC201711090000001" 中的0000001，总位数为7，然后不停的累加，即使日期时间变了，也不停的累加，最好长度设置大一些
Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)+ "\\softNumerical.txt"
);

            tb_sn.Text = softNumerical.GetNumericalOrder();
            svrName = "";
        }

        private void StNodeEditor1_NodeAdded(object sender, STNodeEditorEventArgs e)
        {
            STNode node = (STNode)e.Node;
            node.Tag = _MQTTHelper;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            string iPStr = "192.168.3.225";
            //string iPStr = "127.0.0.1";
            string portStr = "1883";
            string uName = "";// txt用户名.Text.Trim();
            string uPwd = "";// txt密码.Text.Trim();

            int port = Convert.ToInt32(portStr);

            MQTTHelper.SetDefaultCfg(iPStr, port, uName, uPwd);

            if("127.0.0.1".Equals(iPStr))startMQTTSever(iPStr, port);

            Action<ResultData_MQTT> callback = ShowLog;
            Task task = _MQTTHelper.CreateMQTTClientAndStart(iPStr, port, uName, uPwd, callback);

            //panel1.SetAutoScrollMargin(this.stNodeEditor1.Width, this.stNodeEditor1.Height);
            //panel1.AutoScrollMinSize.Width = this.stNodeEditor1.Width;
            //this.stNodeEditor1.Height;
            this.Text = "未命名*";
        }

        private void startMQTTSever(string iPStr, int port)
        {
            Action<ResultData_MQTT> callbackSvr = ShowLogSvr;
            _MQTTSever.CreateMQTTServerAndStart(iPStr, port, true, callbackSvr);
        }

        #region 方法

        private void ShowLogSvr(ResultData_MQTT resultData_MQTT)
        {
            this.Invoke(new Action(() =>
            {
                txt_log.Text += $"\r\nSever：{resultData_MQTT.ResultCode}，信息：{resultData_MQTT.ResultMsg}";
            }));
        }
        /// <summary>
        /// 处理逻辑-展示Log
        /// </summary>
        /// <param name="obj"></param>
        private void ShowLog(ResultData_MQTT resultData_MQTT)
        {
            this.Invoke(new Action(() =>
            {
                txt_log.Text += $"\r\n返回结果：{resultData_MQTT.ResultCode}，返回信息：{resultData_MQTT.ResultMsg}";
                if (resultData_MQTT.ResultCode == 1 && resultData_MQTT.EventType == FlowEngineLib.MQTT.EventTypeEnum.MsgRecv && "SYS.MANUAL".Equals(resultData_MQTT.ResultObject1.ToString()))
                {
                    if(MessageBox.Show("下一步")==DialogResult.OK)
                    {
                        _MQTTHelper.PublishAsync_Client("SYS.CMD", resultData_MQTT.ResultObject2.ToString(), false);
                    }
                }
            }));
        }
        #endregion 方法

        private void btn_save_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "*.stn|*.stn";
            if (sfd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            stNodeEditor1.SaveCanvas(sfd.FileName);
            this.Text = sfd.FileName;
        }

        private void btn_load_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "*.stn|*.stn";
            if (ofd.ShowDialog() != DialogResult.OK) return;
            stNodeEditor1.Nodes.Clear();
            stNodeEditor1.LoadCanvas(ofd.FileName);

            this.Text = ofd.FileName;
        }

        private void btn_subscribe_Click(object sender, EventArgs e)
        {
            string topic = txt主题.Text.Trim();
            if (string.IsNullOrEmpty(topic))
            {
                MessageBox.Show("订阅主题不能为空！");
                return;
            }
            string[] topics = new string[] { "SYS.MANUAL", "SYS.STATUS", "SMU.STATUS", "CAMERA.STATUS", "POI.STATUS", "PG.STATUS", "SPECTRO.STATUS" };
            for(int i = 0; i < topics.Length; i++)
            {
                _MQTTHelper.SubscribeAsync_Client(topics[i]);
            }
            //
        }

        private void btn_unsubscribe_Click(object sender, EventArgs e)
        {
            string topic = txt主题.Text.Trim();
            if (string.IsNullOrEmpty(topic))
            {
                MessageBox.Show("退订主题不能为空！");
                return;
            }
            _MQTTHelper.UnsubscribeAsync_Client(topic);
        }

        private void delToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (stNodeEditor1.ActiveNode == null) return;
            stNodeEditor1.Nodes.Remove(stNodeEditor1.ActiveNode);
        }

        private void btn_start_Click(object sender, EventArgs e)
        {
            if(checkBox1.Checked) tb_sn.Text = softNumerical.GetNumericalOrder();
            CVBaseDataFlow baseEvent = new CVBaseDataFlow(svrName, "Start", tb_sn.Text);
            _MQTTHelper.PublishAsync_Client("SYS.CMD." + textBox1.Text, JsonConvert.SerializeObject(baseEvent), false);
        }

        private void btn_new_Click(object sender, EventArgs e)
        {
            stNodeEditor1.Nodes.Clear();
            this.Text = "未命名*";
        }

        private void btn_pause_Click(object sender, EventArgs e)
        {
            CVBaseDataFlow baseEvent = new CVBaseDataFlow(svrName, "Pause", tb_sn.Text);
            _MQTTHelper.PublishAsync_Client("SYS.CMD." + textBox1.Text, JsonConvert.SerializeObject(baseEvent), false);
        }

        private void btn_stop_Click(object sender, EventArgs e)
        {
            CVBaseDataFlow baseEvent = new CVBaseDataFlow(svrName, "Stop", tb_sn.Text);
            _MQTTHelper.PublishAsync_Client("SYS.CMD." + textBox1.Text, JsonConvert.SerializeObject(baseEvent), false);
        }

        private void btn_id_gen_Click(object sender, EventArgs e)
        {
            tb_sn.Text = softNumerical.GetNumericalOrder();
        }

        private void Form1_SizeChanged(object sender, EventArgs e)
        {
        }

        private void stNodePropertyGrid1_Click(object sender, EventArgs e)
        {

        }

        private void splitContainer2_Panel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
