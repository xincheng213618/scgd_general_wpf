using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Serial
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MotorControlWindow : Window
    {
        public MotorControlWindow()
        {
            InitializeComponent();
            this.Closed += Window_Closed;
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            MotorControl.Close();
        }
        MotorControl MotorControl = MotorControl.GetInstance();

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (MotorControl.Initialized() == 0)
                {
                    MessageBox.Show("已连接");
                    StackPanelMotorState.DataContext = MotorControl.MotorState;
                    button.Content = "已连接";
                }
            }
        }

        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            MotorControl.ReadMotorState();
        }

        private void Button2_Click(object sender, RoutedEventArgs e)
        {
            int speed;
            if (!int.TryParse(TextboxMotorSpeed.Text, out speed))
                speed = 30;

            int length ;
            if (!int.TryParse(TextboxMotorLen.Text, out length))
                length = 360;

            MotorControl.Moveangle(length, speed);
        }

        private void Button3_Click(object sender, RoutedEventArgs e)
        {
            int speed;
            if (!int.TryParse(TextboxMotorSpeed.Text, out speed))
                speed = 30;

            int length;
            if (!int.TryParse(TextboxMotorLen.Text, out length))
                length = 360;

            MotorControl.Moveangle(-length, speed);
        }

        private void Button4_Click(object sender, RoutedEventArgs e)
        {
            MotorControl.RemoveRelateLocation();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {

        }

        private void Button5_Click(object sender, RoutedEventArgs e)
        {
            int speed;
            if (!int.TryParse(TextboxMotorSpeed.Text, out speed))
                speed = 30;

            int length;
            if (!int.TryParse(TextboxMotorLenght.Text, out length))
                length = 360;

            MotorControl.Move(length, speed);
        }

        private void Button6_Click(object sender, RoutedEventArgs e)
        {
            int speed;
            if (!int.TryParse(TextboxMotorSpeed.Text, out speed))
                speed = 30;

            int length;
            if (!int.TryParse(TextboxMotorLenght.Text, out length))
                length = 360;

            MotorControl.Move(-length, speed);
        }

        private void Button7_Click(object sender, RoutedEventArgs e)
        {
            int speed;
            if (!int.TryParse(TextboxMotorSpeed.Text, out speed))
                speed = 30;

            int length;
            if (!int.TryParse(TextboxMotorAb.Text, out length))
                length = 360;

            MotorControl.Move(length - MotorControl.MotorState.RelativePosition, speed);
        }

        private void Button8_Click(object sender, RoutedEventArgs e)
        {
            MotorControl.ReturnZero();
        }

        private async void Button9_Click(object sender, RoutedEventArgs e)
        {
            await MotorControl.CalibrationZero();
        }

        private async void Button11_Click(object sender, RoutedEventArgs e)
        {
            int speed;
            if (!int.TryParse(TextboxMotorSpeed.Text, out speed))
                speed = 30;
            int length;
            if (!int.TryParse(TextboxMotorLenght.Text, out length))
                length = 360;
            bool a = await MotorControl.MoveAsync(length, speed, 2000);
            MessageBox.Show("Test:" + a.ToString());
        }
    }
}
