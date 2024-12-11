using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql;
using log4net;
using NModbus;
using System.Net.Sockets;
using System.Windows;

namespace ProjectKB
{



    public class ModbusControl:ViewModelBase
    {
        private static ILog log = LogManager.GetLogger(typeof(ModbusControl));
        private static ModbusControl _instance;
        private static readonly object _locker = new();
        public static ModbusControl GetInstance() { lock (_locker) { return _instance ??= new ModbusControl(); } }


        public static ModbusConfig Config =>ModbusSetting.Instance.ModbusConfig;

        static ushort registerAddress => Config.RegisterAddress; // 需要监控的寄存器地址
        ushort previousValue;
        public ModbusControl()
        {

        }

        public event EventHandler StatusChanged;

        public bool Status { get => _Status; set { if (value == _Status) return; _Status = value; NotifyPropertyChanged(); StatusChanged?.Invoke(this,new EventArgs()); } }
        private bool _Status ;

        public static bool TestConnect(ModbusConfig Config)
        {
            try
            {
                log.Info($"TestConnet: ModbusConfig:{Config}");

                using (var client = new TcpClient(Config.Host, Config.Port))
                {
                    log.Info($"TestConnet: TcpConnect");

                    var factory = new ModbusFactory();
                    var master = factory.CreateMaster(client);

                    // 读取从站1的寄存器地址100的值
                    ushort[] registers = master.ReadHoldingRegisters(1, registerAddress, 1);
                    ushort currentValue = registers[0];
                    log.Info($"TestConnet: currentValue:{currentValue}");
                    return true;
                }

            }
            catch (Exception ex)
            {
                log.Info($"TestConnet: Error: {ex.Message}");
                return false;
            }
        }

        public bool Connect()
        {
            try
            {

                using (var client = new TcpClient(Config.Host, Config.Port))
                {
                    var factory = new ModbusFactory();
                    var master = factory.CreateMaster(client);
                    // 读取从站1的寄存器地址100的值
                    ushort[] registers = master.ReadHoldingRegisters(1, registerAddress, 1);
                    ushort currentValue = registers[0];
                    log.Info($"{DateTime.Now} registerAddress{registerAddress}: currentValue:{currentValue}");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Status = currentValue == 0;
                        if (currentValue != previousValue)
                        {
                            previousValue = currentValue;
                        }
                    });
                    IsConnect = true;

                    Task.Run(() =>
                    {
                        CheckUpdate();
                    });

                }
            }
            catch (Exception ex)
            {
                log.Info($"{DateTime.Now} Error: {ex.Message}");
                IsConnect = false;
            }

            return IsConnect;
        }

        public event EventHandler MySqlConnectChanged;

        public bool IsConnect { get => _IsConnect; private set { _IsConnect = value; NotifyPropertyChanged(); } }
        private bool _IsConnect;


        public void CheckUpdate()
        {
            while (IsConnect)
            {
                try
                {

                    using (var client = new TcpClient(Config.Host, Config.Port))
                    {
                        var factory = new ModbusFactory();
                        var master = factory.CreateMaster(client);
                        // 读取从站1的寄存器地址100的值
                        ushort[] registers = master.ReadHoldingRegisters(1, registerAddress, 1);
                        ushort currentValue = registers[0];
                        log.Info($"{DateTime.Now} registerAddress{registerAddress}: currentValue:{currentValue}");
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            Status = currentValue == 0;
                            if (currentValue != previousValue)
                            {
                                previousValue = currentValue;
                            }
                        });
                        IsConnect = true;
                    }
                }
                catch (Exception ex)
                {
                    log.Info($"{DateTime.Now} Error: {ex.Message}");
                    IsConnect = false;
                    break;
                }
                Thread.Sleep(1000); // 每秒检查一次
            }
        }

        public bool SetRegisterValue(ushort value)
        {
            try
            {
                using (var client = new TcpClient(Config.Host, Config.Port))
                {
                    var factory = new ModbusFactory();
                    var master = factory.CreateMaster(client);

                    master.WriteSingleRegister(1, registerAddress, value);
                    Console.WriteLine($"Register value set to: {value}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return false;
            }
        }

    }
}
