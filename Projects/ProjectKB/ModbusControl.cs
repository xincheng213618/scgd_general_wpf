using ColorVision.Common.MVVM;
using NModbus;
using System.Net.Sockets;

namespace ProjectKB
{



    public class ModbusControl:ViewModelBase
    {
        public static ModbusConfig Config =>ModbusSetting.Instance.ModbusConfig;

        ushort registerAddress = 0xD0; // 需要监控的寄存器地址
        ushort previousValue;
        public ModbusControl()
        {
            while (true)
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

                        Status = currentValue == 0;
                        if (currentValue != previousValue)
                        {
                            Console.WriteLine($"Register value changed: {currentValue}");
                            // 在此执行你的指令
                            previousValue = currentValue;
                        }
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    // 根据需要添加重试或其他错误处理逻辑
                }
                Thread.Sleep(1000); // 每秒检查一次
            }
        }

        public event EventHandler StatusChanged;

        public bool Status { get => _Status; set { if (value == _Status) return; _Status = value; NotifyPropertyChanged(); StatusChanged?.Invoke(this,new EventArgs()); } }
        private bool _Status ;


        public bool Connet()
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

                    if (currentValue != previousValue)
                    {
                        Console.WriteLine($"Register value changed: {currentValue}");
                        // 在此执行你的指令
                        previousValue = currentValue;
                    }
                    return true;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                // 根据需要添加重试或其他错误处理逻辑
                return false;
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
