using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ColorVisionUpdateRelay
{
    public sealed class MainForm : Form
    {
        private const string DefaultServerHost = "xc213618.ddns.me";
        private const int DefaultPort = 9998;

        private readonly TextBox _ipBox = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Consolas", 10.5f) };
        private readonly TextBox _serverBox = new TextBox { Text = DefaultServerHost, Width = 220 };
        private readonly TextBox _portBox = new TextBox { Text = DefaultPort.ToString(CultureInfo.InvariantCulture), Width = 70 };
        private readonly TextBox _relayIpBox = new TextBox { Width = 160 };
        private readonly Label _clientStatus = new Label { AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
        private readonly Label _relayStatus = new Label { AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
        private readonly TextBox _log = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BackColor = SystemColors.Window };
        private readonly TabControl _tabs = new TabControl { Dock = DockStyle.Fill };
        private Control[] _actionButtons;

        public MainForm()
        {
            Text = "ColorVision 更新跳板工具";
            Font = new Font("Microsoft YaHei UI", 9f);
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(720, 640);
            MinimumSize = new Size(640, 560);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), ColumnCount = 1, RowCount = 4 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            // This machine's IP addresses: on the relay these are what the client machines enter.
            var ipGroup = new GroupBox { Text = "本机 IP 地址（跳板机的 IP 填到客户机上）", Dock = DockStyle.Fill };
            var ipLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            ipLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            ipLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var ipButtons = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
            ipButtons.Controls.Add(MakeButton("刷新", (s, e) => RefreshAll()));
            ipButtons.Controls.Add(MakeButton("复制", (s, e) => CopyIps()));
            ipLayout.Controls.Add(_ipBox, 0, 0);
            ipLayout.Controls.Add(ipButtons, 1, 0);
            ipGroup.Controls.Add(ipLayout);
            root.Controls.Add(ipGroup, 0, 0);

            var serverGroup = new GroupBox { Text = "更新服务器（一般不用改）", Dock = DockStyle.Fill, AutoSize = true };
            var serverRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
            serverRow.Controls.Add(MakeLabel("地址"));
            serverRow.Controls.Add(_serverBox);
            serverRow.Controls.Add(MakeLabel("端口"));
            serverRow.Controls.Add(_portBox);
            serverGroup.Controls.Add(serverRow);
            root.Controls.Add(serverGroup, 0, 1);

            var clientPage = new TabPage("客户机（不能上外网的电脑）") { Padding = new Padding(10) };
            var clientLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            clientLayout.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(660, 0), Text = "在每台需要更新的电脑上运行：让 ColorVision 的更新请求经跳板机转发。只修改本机 hosts 中本工具添加的那一行。" });
            var clientRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 8, 0, 0) };
            clientRow.Controls.Add(MakeLabel("跳板机 IP"));
            clientRow.Controls.Add(_relayIpBox);
            var setClient = MakeButton("设置跳板", async (s, e) => await RunAsync("设置跳板", SetClient));
            var removeClient = MakeButton("移除设置", async (s, e) => await RunAsync("移除设置", RemoveClient));
            var testClient = MakeButton("测试连接", async (s, e) => await RunAsync("测试连接", TestClient));
            clientRow.Controls.AddRange(new Control[] { setClient, removeClient, testClient });
            clientLayout.Controls.Add(clientRow);
            clientLayout.Controls.Add(_clientStatus);
            clientPage.Controls.Add(clientLayout);

            var relayPage = new TabPage("跳板机（能访问更新服务器的电脑）") { Padding = new Padding(10) };
            var relayLayout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            relayLayout.Controls.Add(new Label { AutoSize = true, MaximumSize = new Size(660, 0), Text = "在一台既能访问更新服务器、又能被内网电脑访问的电脑上运行：把本机端口转发到更新服务器，并放行防火墙。完成后把上方的本机 IP 填到客户机。" });
            var relayRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 8, 0, 0) };
            var setRelay = MakeButton("开启转发", async (s, e) => await RunAsync("开启转发", SetRelay));
            var removeRelay = MakeButton("移除转发", async (s, e) => await RunAsync("移除转发", RemoveRelay));
            var testRelay = MakeButton("测试转发", async (s, e) => await RunAsync("测试转发", TestRelay));
            relayRow.Controls.AddRange(new Control[] { setRelay, removeRelay, testRelay });
            relayLayout.Controls.Add(relayRow);
            relayLayout.Controls.Add(_relayStatus);
            relayPage.Controls.Add(relayLayout);

            _tabs.TabPages.Add(clientPage);
            _tabs.TabPages.Add(relayPage);
            root.Controls.Add(_tabs, 0, 2);

            var logGroup = new GroupBox { Text = "执行记录", Dock = DockStyle.Fill };
            logGroup.Controls.Add(_log);
            root.Controls.Add(logGroup, 0, 3);

            _actionButtons = new Control[] { setClient, removeClient, testClient, setRelay, removeRelay, testRelay, _serverBox, _portBox, _relayIpBox };
            Shown += (s, e) =>
            {
                RefreshAll();
                if (RelayOperations.FindForward(Port) != null) _tabs.SelectedIndex = 1;
            };
        }

        // Inputs are captured on the UI thread before each action, because actions run on a worker thread.
        private string ServerHost = DefaultServerHost;
        private int Port = DefaultPort;
        private string RelayInput = "";

        private void CaptureInputs()
        {
            ServerHost = _serverBox.Text.Trim();
            if (!int.TryParse(_portBox.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int port) || port <= 0 || port >= 65536)
            {
                port = DefaultPort;
                _portBox.Text = port.ToString(CultureInfo.InvariantCulture);
            }
            Port = port;
            RelayInput = _relayIpBox.Text.Trim();
        }

        // ---------- client ----------

        private void SetClient()
        {
            string relay = RelayInput;
            if (!RelayOperations.IsValidRelayAddress(relay)) throw new InvalidOperationException("请填写跳板机的 IPv4 地址，例如 192.168.1.20（在跳板机上运行本工具，顶部会显示）。");
            if (RelayOperations.FindForward(Port) != null) throw new InvalidOperationException("本机已开启端口 " + Port + " 的转发（是跳板机）。跳板机自己直连服务器即可，不能再设置跳板，否则会转发给自己。");
            if (!RelayOperations.CanConnect(relay, Port, Log) && !Confirm("现在连不上跳板机 " + relay + ":" + Port + "。\n可能跳板机还没开启转发，或防火墙未放行。\n\n仍然写入设置吗？")) return;
            RelayOperations.SetClientRelay(ServerHost, relay, Log);
            Log("解析 " + ServerHost + " → " + RelayOperations.DescribeResolution(ServerHost));
            RelayOperations.QueryLatestVersion(ServerHost, ServerHost, Port, Log);
        }

        private void RemoveClient()
        {
            RelayOperations.RemoveClientRelay(ServerHost, Log);
            Log("解析 " + ServerHost + " → " + RelayOperations.DescribeResolution(ServerHost));
        }

        private void TestClient()
        {
            string relay = RelayOperations.GetClientRelay(ServerHost);
            Log(relay == null ? "本机未设置跳板，ColorVision 直连更新服务器。" : "本机跳板：" + relay);
            Log("解析 " + ServerHost + " → " + RelayOperations.DescribeResolution(ServerHost));
            RelayOperations.QueryLatestVersion(ServerHost, ServerHost, Port, Log);
        }

        // ---------- relay ----------

        private void SetRelay()
        {
            string loop = RelayOperations.CheckRelayLoop(ServerHost);
            if (loop != null) throw new InvalidOperationException(loop);
            if (!RelayOperations.CanConnect(ServerHost, Port, Log) && !Confirm("本机现在连不上更新服务器 " + ServerHost + ":" + Port + "，开启转发后客户机也无法更新。\n\n仍然开启吗？")) return;
            RelayOperations.EnableForward(ServerHost, Port, Log);
            RelayOperations.QueryLatestVersion("127.0.0.1", ServerHost, Port, Log);
            Log("完成。在每台客户机的“客户机”页填写以下任一能互通的 IP：");
            foreach (string ip in RelayOperations.GetLocalIPv4()) Log("  " + ip);
        }

        private void RemoveRelay() => RelayOperations.DisableForward(Port, Log);

        private void TestRelay()
        {
            PortProxyEntry entry = RelayOperations.FindForward(Port);
            Log(entry == null ? "本机没有端口 " + Port + " 的转发。" : "端口转发：" + entry);
            Log("防火墙规则：" + (RelayOperations.FirewallRuleExists() ? "已放行" : "不存在"));
            Log("IP Helper 服务：" + RelayOperations.DescribeIpHelper());
            RelayOperations.CanConnect(ServerHost, Port, Log);
            if (entry != null) RelayOperations.QueryLatestVersion("127.0.0.1", ServerHost, Port, Log);
        }

        // ---------- shared ----------

        private void RefreshAll()
        {
            CaptureInputs();
            var ips = RelayOperations.GetLocalIPv4();
            _ipBox.Text = ips.Count == 0 ? "未找到已连接的 IPv4 网卡" : string.Join(Environment.NewLine, ips);
            string relay = null;
            try { relay = RelayOperations.GetClientRelay(ServerHost); } catch (Exception ex) { Log("读取 hosts 失败：" + ex.Message); }
            _clientStatus.Text = relay == null ? "当前状态：未设置跳板（直连更新服务器）" : "当前状态：已设置跳板 " + relay;
            if (relay != null && _relayIpBox.Text.Trim().Length == 0) _relayIpBox.Text = relay;
            PortProxyEntry entry = RelayOperations.FindForward(Port);
            _relayStatus.Text = entry == null ? "当前状态：未开启转发" : "当前状态：已开启转发 " + entry + "；防火墙" + (RelayOperations.FirewallRuleExists() ? "已放行" : "未放行");
        }

        private void CopyIps()
        {
            string ips = string.Join(Environment.NewLine, RelayOperations.GetLocalIPv4().Select(l => l.Split(' ')[0]));
            if (ips.Length == 0) return;
            Clipboard.SetText(ips);
            Log("已复制本机 IP。");
        }

        private async Task RunAsync(string title, Action action)
        {
            CaptureInputs();
            if (string.IsNullOrEmpty(ServerHost)) { MessageBox.Show(this, "请填写更新服务器地址。", title); return; }
            foreach (Control control in _actionButtons) control.Enabled = false;
            Log("==== " + title + " ====");
            try { await Task.Run(action); }
            catch (Exception ex) { Log("失败：" + ex.Message); MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            finally
            {
                foreach (Control control in _actionButtons) control.Enabled = true;
                RefreshAll();
            }
        }

        private void Log(string message)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(Log), message); return; }
            _log.AppendText(DateTime.Now.ToString("HH:mm:ss ", CultureInfo.InvariantCulture) + message + Environment.NewLine);
        }

        private bool Confirm(string message) => Invoke(() => MessageBox.Show(this, message, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes);

        private T Invoke<T>(Func<T> func) => InvokeRequired ? (T)base.Invoke(func) : func();

        private static Label MakeLabel(string text) => new Label { Text = text, AutoSize = true, Padding = new Padding(0, 6, 0, 0) };

        private static Button MakeButton(string text, EventHandler click)
        {
            var button = new Button { Text = text, AutoSize = true, MinimumSize = new Size(84, 28) };
            button.Click += click;
            return button;
        }
    }
}
