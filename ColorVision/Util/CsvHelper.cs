#pragma warning disable CS8602,CA1806,CA2201
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ColorVision.Util
{
    class CsvHelper
    {
        /// <summary>
        /// 将DataTable中数据写入到CSV文件中
        /// </summary>
        /// <param name="dt">提供保存数据的DataTable</param>
        /// <param name="fileName">CSV的文件路径</param>
        public static void SaveCSV(DataTable dt, string fullPath)
        {
            FileInfo fi = new FileInfo(fullPath);
            if (!fi.Directory.Exists)
            {
                fi.Directory.Create();
            }
            FileStream fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
            //StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.Default);
            StreamWriter sw = new StreamWriter(fs, Encoding.UTF8);
            string data = "";
            //写出列名称
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                data += dt.Columns[i].ColumnName.ToString();
                if (i < dt.Columns.Count - 1)
                {
                    data += ",";
                }
            }
            sw.WriteLine(data);
            //写出各行数据
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                data = "";
                Console.WriteLine("save row: " + i.ToString());
                for (int j = 0; j < dt.Columns.Count; j++)
                {
                    string str = dt.Rows[i][j].ToString();
                    str = str.Replace("\"", "\"\"");//替换英文冒号 英文冒号需要换成两个冒号
                    if (str.Contains(',') || str.Contains('"')
                        || str.Contains('\r') || str.Contains('\n')) //含逗号 冒号 换行符的需要放到引号中
                    {
                        str = string.Format("\"{0}\"", str);
                    }

                    data += str;
                    if (j < dt.Columns.Count - 1)
                    {
                        data += ",";
                    }
                }
                sw.WriteLine(data);
            }
            sw.Close();
            fs.Close();
            /*
            DialogResult result = MessageBox.Show("CSV文件保存成功！");
            if (result == DialogResult.OK)
            {
                System.Diagnostics.Process.Start("explorer.exe", Common.PATH_LANG);
            }
            */
        }

        /// <summary>
        /// 将CSV文件的数据读取到DataTable中
        /// </summary>
        /// <param name="fileName">CSV文件路径</param>
        /// <returns>返回读取了CSV数据的DataTable</returns>
        public static DataTable OpenCSV(string filePath)
        {
            Encoding encoding = GetType(filePath); //Encoding.ASCII;//
            DataTable dt = new DataTable();
            FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            //StreamReader sr = new StreamReader(fs, Encoding.UTF8);
            StreamReader sr = new StreamReader(fs, encoding);
            //string fileContent = sr.ReadToEnd();
            //encoding = sr.CurrentEncoding;
            //记录每次读取的一行记录
            string strLine = "";
            //记录每行记录中的各字段内容
            string[] aryLine = null;
            string[] tableHead = null;
            //标示列数
            int columnCount = 0;
            //标示是否是读取的第一行
            bool IsFirst = true;
            //逐行读取CSV中的数据
            while ((strLine = sr.ReadLine()) != null)
            {
                //strLine = Common.ConvertStringUTF8(strLine, encoding);
                //strLine = Common.ConvertStringUTF8(strLine);

                if (IsFirst == true)
                {
                    tableHead = strLine.Split(',');
                    IsFirst = false;
                    columnCount = tableHead.Length;
                    //创建列
                    for (int i = 0; i < columnCount; i++)
                    {
                        DataColumn dc = new DataColumn(tableHead[i]);
                        dt.Columns.Add(dc);
                    }
                }
                else
                {
                    aryLine = strLine.Split(',');
                    DataRow dr = dt.NewRow();
                    for (int j = 0; j < columnCount; j++)
                    {
                        dr[j] = aryLine[j];
                    }
                    dt.Rows.Add(dr);
                }
            }
            if (aryLine != null && aryLine.Length > 0)
            {
                dt.DefaultView.Sort = tableHead[0] + " " + "asc";
            }

            sr.Close();
            fs.Close();
            return dt;
        }

        /// 给定文件的路径，读取文件的二进制数据，判断文件的编码类型
        /// <param name="FILE_NAME">文件路径</param>
        /// <returns>文件的编码类型</returns>

        public static Encoding GetType(string FILE_NAME)
        {
            FileStream fs = new FileStream(FILE_NAME, FileMode.Open,
                FileAccess.Read);
            Encoding r = GetType(fs);
            fs.Close();
            return r;
        }

        /// 通过给定的文件流，判断文件的编码类型
        /// <param name="fs">文件流</param>
        /// <returns>文件的编码类型</returns>
        public static Encoding GetType(FileStream fs)
        {
            byte[] Unicode = new byte[] { 0xFF, 0xFE, 0x41 };
            byte[] UnicodeBIG = new byte[] { 0xFE, 0xFF, 0x00 };
            byte[] UTF8 = new byte[] { 0xEF, 0xBB, 0xBF }; //带BOM
            Encoding reVal = Encoding.Default;

            BinaryReader r = new BinaryReader(fs, Encoding.Default);
            int i;
            int.TryParse(fs.Length.ToString(), out i);
            byte[] ss = r.ReadBytes(i);
            if (IsUTF8Bytes(ss) || (ss[0] == 0xEF && ss[1] == 0xBB && ss[2] == 0xBF))
            {
                reVal = Encoding.UTF8;
            }
            else if (ss[0] == 0xFE && ss[1] == 0xFF && ss[2] == 0x00)
            {
                reVal = Encoding.BigEndianUnicode;
            }
            else if (ss[0] == 0xFF && ss[1] == 0xFE && ss[2] == 0x41)
            {
                reVal = Encoding.Unicode;
            }
            r.Close();
            return reVal;
        }

        /// 判断是否是不带 BOM 的 UTF8 格式
        /// <param name="data"></param>
        /// <returns></returns>
        private static bool IsUTF8Bytes(byte[] data)
        {
            int charByteCounter = 1;  //计算当前正分析的字符应还有的字节数
            byte curByte; //当前分析的字节.
            for (int i = 0; i < data.Length; i++)
            {
                curByte = data[i];
                if (charByteCounter == 1)
                {
                    if (curByte >= 0x80)
                    {
                        //判断当前
                        while (((curByte <<= 1) & 0x80) != 0)
                        {
                            charByteCounter++;
                        }
                        //标记位首位若为非0 则至少以2个1开始 如:110XXXXX...........1111110X　
                        if (charByteCounter == 1 || charByteCounter > 6)
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    //若是UTF-8 此时第一位必须为1
                    if ((curByte & 0xC0) != 0x80)
                    {
                        return false;
                    }
                    charByteCounter--;
                }
            }
            if (charByteCounter > 1)
            {
                throw new Exception(message: "非预期的byte格式");
            }
            return true;
        }
        ///// <summary>
        ///// 导出DataGrid数据到Excel
        ///// </summary>
        ///// <param name="withHeaders">是否需要表头</param>
        ///// <param name="grid">DataGrid</param>
        ///// <param name="dataBind"></param>
        ///// <returns>Excel内容字符串</returns>
        //public static string ExportDataGrid(bool withHeaders, System.Windows.Controls.DataGrid grid, bool dataBind)
        //{
        //    try
        //    {
        //        var strBuilder = new System.Text.StringBuilder();
        //        var source = (grid.ItemsSource as System.Collections.IList);
        //        if (source == null) return "";
        //        var headers = new List<string>();
        //        List<string> bt = new List<string>();

        //        foreach (var hr in grid.Columns)
        //        {
        //            //   DataGridTextColumn textcol = hr. as DataGridTextColumn;
        //            headers.Add(hr.Header.ToString());
        //            if (hr is DataGridTextColumn)//列绑定数据
        //            {
        //                DataGridTextColumn textcol = hr as DataGridTextColumn;
        //                if (textcol != null)
        //                    bt.Add((textcol.Binding as Binding).Path.Path.ToString());        //获取绑定源      

        //            }
        //            else if (hr is DataGridTemplateColumn)
        //            {
        //                if (hr.Header.Equals("操作"))
        //                    bt.Add("Id");
        //            }
        //            else
        //            {

        //            }
        //        }
        //        strBuilder.Append(String.Join(",", headers.ToArray())).Append("\r\n");
        //        foreach (var data in source)
        //        {
        //            var csvRow = new List<string>();
        //            foreach (var ab in bt)
        //            {
        //                string s = ReflectionUtil.GetProperty(data, ab).ToString();
        //                if (s != null)
        //                {
        //                    csvRow.Add(FormatCsvField(s));
        //                }
        //                else
        //                {
        //                    csvRow.Add("\t");
        //                }
        //            }
        //            strBuilder.Append(String.Join(",", csvRow.ToArray())).Append("\r\n");
        //            // strBuilder.Append(String.Join(",", csvRow.ToArray())).Append("\t");
        //        }
        //        return strBuilder.ToString();
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show("error");
        //        return "";
        //    }
        //}



        ///// <summary>
        ///// 导出DataGrid数据到Excel为CVS文件
        ///// 使用utf8编码 中文是乱码 改用Unicode编码
        ///// 
        ///// </summary>
        ///// <param name="withHeaders">是否带列头</param>
        ///// <param name="grid">DataGrid</param>
        //public static void ExportDataGridSaveAs(bool withHeaders, System.Windows.Controls.DataGrid grid)
        //{
        //    try
        //    {
        //        string data = ExportDataGrid(true, grid, true);
        //        var sfd = new Microsoft.Win32.SaveFileDialog
        //        {
        //            DefaultExt = "csv",
        //            Filter = "CSV Files (*.csv)|*.csv|All files (*.*)|*.*",
        //            FilterIndex = 1
        //        };
        //        if (sfd.ShowDialog() == true)
        //        {
        //            using (Stream stream = sfd.OpenFile())
        //            {
        //                using (var writer = new StreamWriter(stream, System.Text.Encoding.Unicode))
        //                {
        //                    data = data.Replace(",", "\t");
        //                    writer.Write(data);
        //                    writer.Close();
        //                }
        //                stream.Close();
        //            }
        //            MessageBox.Show("导出成功！");
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show("error");
        //    }
        //}
    }


}
