using ColorVision.Common.MVVM;
using System.ComponentModel;

namespace ProjectARVRPro.Process
{
    /// <summary>
    /// 步间通信动作类型
    /// </summary>
    public enum InterStepActionType
    {
        /// <summary>无动作</summary>
        [Description("无")]
        None,
        /// <summary>Socket 通信</summary>
        [Description("Socket")]
        Socket,
        /// <summary>串口通信</summary>
        [Description("串口")]
        SerialPort,
        /// <summary>通过现有 SwitchPG 协议切图</summary>
        [Description("SwitchPG")]
        SwitchPG,
        /// <summary>简单延时</summary>
        [Description("延时")]
        Delay
    }

    /// <summary>
    /// 步间通信动作 — 在执行下一个流程之前发送的通信指令
    /// </summary>
    public class InterStepAction : ViewModelBase
    {
        /// <summary>
        /// 是否启用此步间动作
        /// </summary>
        [DisplayName("启用")]
        public bool IsEnabled { get => _IsEnabled; set { if (_IsEnabled != value) { _IsEnabled = value; OnPropertyChanged(); } } }
        private bool _IsEnabled;

        /// <summary>
        /// 通信类型
        /// </summary>
        [DisplayName("动作类型")]
        public InterStepActionType ActionType { get => _ActionType; set { if (_ActionType != value) { _ActionType = value; OnPropertyChanged(); } } }
        private InterStepActionType _ActionType = InterStepActionType.None;

        /// <summary>
        /// 发送的指令内容
        /// </summary>
        [DisplayName("发送指令")]
        public string Command { get => _Command; set { if (_Command != value) { _Command = value; OnPropertyChanged(); } } }
        private string _Command;

        /// <summary>
        /// 期望的应答内容（空则不等待应答）
        /// </summary>
        [DisplayName("期望应答")]
        public string ExpectedResponse { get => _ExpectedResponse; set { if (_ExpectedResponse != value) { _ExpectedResponse = value; OnPropertyChanged(); } } }
        private string _ExpectedResponse;

        /// <summary>
        /// 超时时间（毫秒）
        /// </summary>
        [DisplayName("超时(ms)")]
        public int TimeoutMs { get => _TimeoutMs; set { if (_TimeoutMs != value) { _TimeoutMs = value; OnPropertyChanged(); } } }
        private int _TimeoutMs = 5000;

        /// <summary>
        /// Socket 目标地址
        /// </summary>
        [DisplayName("Socket地址")]
        public string Host { get => _Host; set { if (_Host != value) { _Host = value; OnPropertyChanged(); } } }
        private string _Host;

        /// <summary>
        /// Socket 端口
        /// </summary>
        [DisplayName("Socket端口")]
        public int Port { get => _Port; set { if (_Port != value) { _Port = value; OnPropertyChanged(); } } }
        private int _Port;

        /// <summary>
        /// 串口名称
        /// </summary>
        [DisplayName("串口名称")]
        public string SerialPortName { get => _SerialPortName; set { if (_SerialPortName != value) { _SerialPortName = value; OnPropertyChanged(); } } }
        private string _SerialPortName;

        /// <summary>
        /// 串口波特率
        /// </summary>
        [DisplayName("波特率")]
        public int BaudRate { get => _BaudRate; set { if (_BaudRate != value) { _BaudRate = value; OnPropertyChanged(); } } }
        private int _BaudRate = 9600;

        /// <summary>
        /// 创建深拷贝
        /// </summary>
        public InterStepAction Clone()
        {
            return new InterStepAction
            {
                IsEnabled = IsEnabled,
                ActionType = ActionType,
                Command = Command,
                ExpectedResponse = ExpectedResponse,
                TimeoutMs = TimeoutMs,
                Host = Host,
                Port = Port,
                SerialPortName = SerialPortName,
                BaudRate = BaudRate
            };
        }
    }
}
