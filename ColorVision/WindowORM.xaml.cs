using ColorVision.MySql;
using ColorVision.SettingUp;
using ColorVision.Template;
using MySql.Data.MySqlClient;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
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
    /// t_scgd_sys_dictionary_mod_master 
    /// </summary>
    public class DictionaryModMaster
    {
        public int DdId { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public int? Pid { get; set; }
        public string Pcode { get; set; }
        public DateTime? CreateDate { get; set; }
        public bool IsEnable { get; set; }
        public bool IsDelete { get; set; }
        public string Remark { get; set; }
    }



    /// <summary>
    /// WindowORM.xaml 的交互逻辑
    /// </summary>
    public partial class WindowORM : Window
    {
        public WindowORM()
        {
            InitializeComponent();
        }
        MySqlControl MySqlControl;
        MySqlConnection connection;

        private void Window_Initialized(object sender, EventArgs e)
        {
            MySqlControl = MySqlControl.GetInstance();

            if (!MySqlControl.Open())
                MessageBox.Show("数据库连接失败");
            connection = MySqlControl.GetInstance().MySqlConnection;

        }



        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //从数据库中检索图像并显示在Image控件中
            //cmd = new MySqlCommand("SELECT * FROM image", connection);
            //MySqlDataReader reader = cmd.ExecuteReader();
            //if (reader.Read())
            //{
            //    byte[] imageBytes = (byte[])reader["image"];
            //    BitmapImage image = new BitmapImage();
            //    using (MemoryStream stream = new MemoryStream(imageBytes))
            //    {
            //        image.BeginInit();
            //        image.CacheOption = BitmapCacheOption.OnLoad;
            //        image.StreamSource = stream;
            //        image.EndInit();
            //    }
            //    TestImage.Source = image;
            //}
            //connection.Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var keyValuePair = TemplateControl.GetInstance().PoiParams[0];


            string Name = keyValuePair.Key;
            PoiParam poiParam = keyValuePair.Value;

            Dictionary<string, object> keyValuePairs = new Dictionary<string, object>()
            {
                { "name", Name },
                { "type",poiParam.Type},
                { "width", poiParam.Width },
                {"height", poiParam.Height},
                {"create_date", DateTime.Now},
                {"is_enable", 1},
                {"is_delete", 0},
                {"remark", ""}
            };

            if (Add("t_scgd_cfg_poi_master", keyValuePairs) == 1)
            {
                MessageBox.Show("添加表信息成功");

                foreach (var item in poiParam.PoiPoints)
                {
                    Dictionary<string, object> keyValuePairs1 = new Dictionary<string, object>()
                    {
                        { "type",poiParam.Type},
                        { "width", poiParam.Width },
                        {"height", poiParam.Height},
                        {"create_date", DateTime.Now},
                        {"is_enable", 1},
                        {"is_delete", 0},
                        {"remark", ""}
                    };

                }


            }
            else
            {
                MessageBox.Show("添加失败");
            }




            //Dictionary<string, object> keyValuePairs = new Dictionary<string, object>()
            //{
            //    { "code", "your_code" },
            //    { "name", "your_name" },
            //    { "pid", 1 },
            //    { "pcode", "your_pcode" },
            //    {"create_date", DateTime.Now},
            //    {"is_enable", 1},
            //    {"is_delete", 0},
            //    {"remark", "your_remark"}
            //};
            //if (Add(keyValuePairs) == 1)
            //{
            //    MessageBox.Show("添加成功");
            //}
            //else
            //{
            //    MessageBox.Show("添加失败");
            //}
        }


        public int Add(string TablesName,Dictionary<string, object> keyValuePairs)
        {
            string a1 = string.Empty;
            string a2 = string.Empty;

            foreach (var item in keyValuePairs)
            {
                a1 += item.Key + ",";
                a2 += "@" + item.Key + ",";
            }
            a1 = a1[..^1];
            a2 = a2[..^1];

            string insertQuery = $"INSERT INTO {TablesName} ({a1}) VALUES ({a2})";
            using MySqlCommand command = new MySqlCommand(insertQuery, connection);
            foreach (var item in keyValuePairs)
                command.Parameters.AddWithValue("@" + item.Key, item.Value);
            int rowsAffected = command.ExecuteNonQuery();
            return rowsAffected;
        }


        public int Add(Dictionary<string,object> keyValuePairs)
        {
            string a1 = string.Empty;
            string a2 = string.Empty;

            foreach (var item in keyValuePairs)
            {
                a1 += item.Key +",";
                a2 += "@"+item.Key + ",";
            }
            a1 = a1[..^1];
            a2 = a2[..^1];

            string insertQuery = $"INSERT INTO t_scgd_sys_dictionary_mod_master ({a1}) VALUES ({a2})";
            using MySqlCommand command = new MySqlCommand(insertQuery, connection);
            foreach (var item in keyValuePairs)
                command.Parameters.AddWithValue("@" + item.Key, item.Value);
            int rowsAffected = command.ExecuteNonQuery();
            return rowsAffected;
        }




        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            DictionaryModMaster item = items.Last();
            if (Delete(items.Last().DdId) == 1)
            {
                items.Remove(item);
                MessageBox.Show($"删除DdId{items.Last().DdId}成功");
            }
            else
            {
                MessageBox.Show($"删除DdId{items.Last().DdId}失败");
            }
        }
        List<DictionaryModMaster> items;
        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            Update();
        }


        public static List<DictionaryModMaster> DataSetToList(DataSet dataSet)
        {
            List<DictionaryModMaster> personList = new List<DictionaryModMaster>();

            if (dataSet != null && dataSet.Tables.Count > 0)
            {
                DataTable dataTable = dataSet.Tables[0];

                foreach (DataRow row in dataTable.Rows)
                {
                    DictionaryModMaster person = new DictionaryModMaster();

                    person.DdId = Convert.ToInt32(row["dd_id"]);
                    personList.Add(person);
                }
            }

            return personList;
        }


        public int Update()
        {
            string updateQuery = "UPDATE t_scgd_sys_dictionary_mod_master SET name = @name WHERE dd_id = @ddId";
            using MySqlCommand command = new MySqlCommand(updateQuery, connection);
            command.Parameters.AddWithValue("@name", "new_name");
            command.Parameters.AddWithValue("@ddId", 12);
            int rowsAffected = command.ExecuteNonQuery();
            return rowsAffected;
        }
        public int Delete(int ddid)
        {
            string deleteQuery = "DELETE FROM t_scgd_sys_dictionary_mod_master WHERE dd_id = @ddId";
            using MySqlCommand command = new MySqlCommand(deleteQuery, connection);
            command.Parameters.AddWithValue("@ddId", ddid);
            int rowsAffected = command.ExecuteNonQuery();
            return rowsAffected;
        }
        public DataSet dataSet { get; set; } = new DataSet();
        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            List<Dictionary<string, object>> myList = new List<Dictionary<string, object>>();
            string selectQuery = "SELECT * FROM t_scgd_sys_dictionary_mod_master";

            // 设置DataGrid的数据源
            using (MySqlCommand command = new MySqlCommand(selectQuery, connection))
            {

                using (MySqlDataAdapter adapter = new MySqlDataAdapter(command))
                {
                    // 创建数据集
                    dataSet = new DataSet();

                    // 填充数据集
                    adapter.Fill(dataSet);
                    // 将数据集设置为DataGrid的数据源
                    dataGrid.ItemsSource = dataSet.Tables[0].DefaultView;

                    items =DataSetToList(dataSet);
                }
            }
        }
    }
}
