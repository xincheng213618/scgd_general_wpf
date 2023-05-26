using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;


namespace ColorVision
{
    /// <summary>
    /// WindowORM.xaml 的交互逻辑
    /// </summary>
    public partial class WindowORM : Window
    {
        public WindowORM()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string connStr = "server=192.168.3.250;uid=root;pwd=123456@cv;database=cv";
            connStr = "server=127.0.0.1;uid=root;pwd=xincheng;database=color_vision";

            MySqlConnection conn = new MySqlConnection() { ConnectionString = connStr };
            conn.Open();
            MySqlCommand cmd;
           //将图像插入到数据库中
             byte[] imageData = File.ReadAllBytes("C:\\Users\\17917\\Desktop\\1.tif");
            //cmd = new MySqlCommand("INSERT INTO image (image) VALUES (@image)", conn);
            //cmd.Parameters.AddWithValue("@image", imageData);
            //cmd.ExecuteNonQuery();

            //从数据库中检索图像并显示在Image控件中
            cmd = new MySqlCommand("SELECT * FROM image", conn);
            MySqlDataReader reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                byte[] imageBytes = (byte[])reader["image"];
                BitmapImage image = new BitmapImage();
                using (MemoryStream stream = new MemoryStream(imageBytes))
                {
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream;
                    image.EndInit();
                }
                TestImage.Source = image;
            }
            conn.Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {

        }
    }
}
