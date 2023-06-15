using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FlowEngineDesign
{
    public partial class Form2 : Form
    {
        private FlowEngineLib.MQTTHelper _MQTTHelper = new FlowEngineLib.MQTTHelper();
        private FlowEngineLib.STNodeLoader loader;
        private string svrName;
        private string loadedFileName;
        private string serialNumber;
        public Form2()
        {
            InitializeComponent();
            loader = new FlowEngineLib.STNodeLoader("FlowEngineLib.dll");
            svrName = "";
        }

        private void Form2_Load(object sender, EventArgs e)
        {
            //string iPStr = "192.168.3.225";
            string iPStr = "127.0.0.1";
            string portStr = "1883";
            string uName = "";// txt用户名.Text.Trim();
            string uPwd = "";// txt密码.Text.Trim();

            int port = Convert.ToInt32(portStr);

            FlowEngineLib.MQTTHelper.SetDefaultCfg(iPStr, port, uName, uPwd);

            Task task = _MQTTHelper.CreateMQTTClientAndStart(iPStr, port, uName, uPwd, ShowLog);
            Task task1 = task.ContinueWith(ended) ;
        }

        private void ended(Task t)
        {
        }

        private void button_load_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "*.stn|*.stn";
            if (ofd.ShowDialog() != DialogResult.OK) return;

            loader.Load(ofd.FileName);
            this.Text = ofd.FileName;
            this.loadedFileName = ofd.FileName;

            _MQTTHelper.SubscribeAsync_Client("SYS.STATUS."+ loader.GetStartNodeName());
        }

        /// <summary>
        /// 处理逻辑-展示Log
        /// </summary>
        /// <param name="obj"></param>
        private void ShowLog(FlowEngineLib.ResultData_MQTT resultData_MQTT)
        {
            this.Invoke(new Action(() =>
            {
                txt_log.Text += $"\r\n返回结果：{resultData_MQTT.ResultCode}，返回信息：{resultData_MQTT.ResultMsg}";
                if (resultData_MQTT.ResultCode == 1 && resultData_MQTT.EventType == FlowEngineLib.MQTT.EventTypeEnum.MsgRecv && ("SYS.STATUS."+ loader.GetStartNodeName()).Equals(resultData_MQTT.ResultObject1.ToString()))
                {
                    FlowEngineLib.CVBaseDataFlow baseEvent = JsonConvert.DeserializeObject<FlowEngineLib.CVBaseDataFlow>(resultData_MQTT.ResultObject2.ToString());
                    if(baseEvent != null && !baseEvent.EventName.Equals("Runing"))
                    {
                        this.button1.Enabled = true;
                    }
                }
            }));
        }

        private void button1_Click(object sender, EventArgs e)
        {
            serialNumber = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            this.textBox1.Text = serialNumber;
            FlowEngineLib.CVBaseDataFlow baseEvent = new FlowEngineLib.CVBaseDataFlow(svrName, "Start", serialNumber);
            _MQTTHelper.PublishAsync_Client(GetTopic(), JsonConvert.SerializeObject(baseEvent), false);
            this.button1.Enabled = false;
        }

        private string GetTopic()
        {
            return "SYS.CMD." + loader.GetStartNodeName();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Form1 form = new Form1();
            if(!string.IsNullOrEmpty(loadedFileName)) {
                form.loadedFileName = loadedFileName;
            }
            form.Show();
        }

        private void button_stop_Click(object sender, EventArgs e)
        {
            FlowEngineLib.CVBaseDataFlow baseEvent = new FlowEngineLib.CVBaseDataFlow(svrName, "Stop", serialNumber);
            _MQTTHelper.PublishAsync_Client(GetTopic(), JsonConvert.SerializeObject(baseEvent), false);
        }
    }
}
