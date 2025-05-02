using ColorVision.Common.MVVM;
using log4net;
using NModbus;
using System.Net.Sockets;
using System.Windows;

namespace ProjectKB.Modbus
{

    public class ModbusControl : ViewModelBase
    {
        private static ILog log = LogManager.GetLogger(typeof(ModbusControl));
        private static ModbusControl _instance;
        private static readonly object _locker = new();
        public static ModbusControl GetInstance() { lock (_locker) { return _instance ??= new ModbusControl(); } }

        public static ModbusConfig Config => ModbusSetting.Instance.ModbusConfig;

        static ushort registerAddress => Config.RegisterAddress; // 需要监控的寄存器地址
        ushort previousValue;
        public ModbusControl()
        {
            Task.Run(() =>
            {
                CheckUpdate();
            });
        }

        public event EventHandler StatusChanged;

        public ushort CurrentValue { get => _CurrentValue; set { if (value == _CurrentValue) return; _CurrentValue = value; NotifyPropertyChanged(); StatusChanged?.Invoke(this, new EventArgs()); } }
        private ushort _CurrentValue;

        public static bool TestConnect(ModbusConfig Config)
        {
            try
            {
                log.Debug($"TestConnet: ModbusConfig:{Config}");

                using (var client = new TcpClient(Config.Host, Config.Port))
                {
                    log.Debug($"TestConnet: TcpConnect");

                    var factory = new ModbusFactory();
                    var master = factory.CreateMaster(client);

                    // 读取从站1的寄存器地址100的值
                    ushort[] registers = master.ReadHoldingRegisters(1, registerAddress, 1);
                    ushort currentValue = registers[0];
                    log.Debug($"TestConnet: currentValue:{currentValue}");
                    return true;
                }

            }
            catch (Exception ex)
            {
                log.Error($"TestConnet: Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> Connect()
        {
            try
            {
                using (var client = new TcpClient())
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
                    {
                        var connectTask = client.ConnectAsync(Config.Host, Config.Port);

                        if (await Task.WhenAny(connectTask, Task.Delay(Timeout.Infinite, cts.Token)) != connectTask)
                        {
                            throw new TimeoutException("Failed to connect within the timeout period.");
                        }

                        // Set a 3-second timeout for send and receive operations
                        client.ReceiveTimeout = 1000;
                        client.SendTimeout = 1000;

                        var factory = new ModbusFactory();
                        var master = factory.CreateMaster(client);

                        // Read register value
                        ushort[] registers = await Task.Run(() => master.ReadHoldingRegisters(1, registerAddress, 1));
                        ushort currentValue = registers[0];
                        log.Debug($"{DateTime.Now} registerAddress{registerAddress}: currentValue:{currentValue}");

                        Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            CurrentValue = currentValue;
                            if (currentValue != previousValue)
                            {
                                previousValue = currentValue;
                            }
                        });

                        IsConnect = true;
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error($"{DateTime.Now} Error: {ex.Message}");
                IsConnect = false;
            }

            return IsConnect;
        }

        public event EventHandler MySqlConnectChanged;

        public bool IsConnect { get => _IsConnect; private set { _IsConnect = value; NotifyPropertyChanged(); } }
        private bool _IsConnect;

        bool IsRun = false;

        public async void CheckUpdate()
        {
            while (true)
            {
                if (IsConnect)
                {
                    try
                    {
                        if (!IsRun)
                        {
                            using (var client = new TcpClient())
                            {
                                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
                                {
                                    var connectTask = client.ConnectAsync(Config.Host, Config.Port);

                                    if (await Task.WhenAny(connectTask, Task.Delay(Timeout.Infinite, cts.Token)) != connectTask)
                                    {
                                        throw new TimeoutException("Failed to connect within the timeout period.");
                                    }

                                    // Set a 3-second timeout for send and receive operations
                                    client.ReceiveTimeout = 3000;
                                    client.SendTimeout = 3000;

                                    var factory = new ModbusFactory();
                                    var master = factory.CreateMaster(client);

                                    // Read register value
                                    ushort[] registers = await Task.Run(() => master.ReadHoldingRegisters(1, registerAddress, 1));
                                    ushort currentValue = registers[0];
                                    log.Debug($"{DateTime.Now} registerAddress{registerAddress}: currentValue:{currentValue}");

                                    Application.Current.Dispatcher.BeginInvoke(() =>
                                    {
                                        CurrentValue = currentValue;
                                        if (currentValue != previousValue)
                                        {
                                            previousValue = currentValue;
                                        }
                                    });

                                    IsConnect = true;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error($"{DateTime.Now} Error: {ex.Message}");
                        IsConnect = false;
                        break;
                    }
                }
                await Task.Delay(1000); // 每秒检查一次   
            }
        }

        public async Task<bool> SetRegisterValue(ushort value)
        {
            try
            {
                IsRun = true;
                using (var client = new TcpClient())
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
                    {
                        var connectTask = client.ConnectAsync(Config.Host, Config.Port);

                        if (await Task.WhenAny(connectTask, Task.Delay(Timeout.Infinite, cts.Token)) != connectTask)
                        {
                            throw new TimeoutException("Failed to connect within the timeout period.");
                        }

                        // Set a 3-second timeout for send and receive operations
                        client.ReceiveTimeout = 1000;
                        client.SendTimeout = 1000;

                        var factory = new ModbusFactory();
                        var master = factory.CreateMaster(client);

                        await Task.Run(() => master.WriteSingleRegister(1, registerAddress, value));
                        Console.WriteLine($"Register value set to: {value}");
                    }
                }
                IsRun = false;
                return true;
            }
            catch (Exception ex)
            {
                IsRun = false;
                Console.WriteLine($"Error: {ex.Message}");
                return false;
            }
        }

    }
}
