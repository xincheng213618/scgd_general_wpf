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
    public class CsvHelper
    {
        /// <summary>
        /// 导出报表为Csv
        /// </summary>
        /// <param name="dt">DataTable</param>
        /// <param name="strFilePath">物理路径</param>
        /// <param name="tableheader">表头</param>
        /// <param name="columname">字段标题,逗号分隔</param>
        public static bool Dt2csv(DataTable dt, string strFilePath, string tableheader, string columname)
        {
            try
            {
                string strBufferLine = "";
                StreamWriter strmWriterObj = new StreamWriter(strFilePath, false, Encoding.UTF8);
                strmWriterObj.WriteLine(tableheader);
                strmWriterObj.WriteLine(columname);
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    strBufferLine = "";
                    for (int j = 0; j < dt.Columns.Count; j++)
                    {
                        if (j > 0)
                            strBufferLine += ",";
                        strBufferLine += dt.Rows[i][j].ToString();
                    }
                    strmWriterObj.WriteLine(strBufferLine);
                }
                strmWriterObj.Close();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 将Csv读入DataTable
        /// </summary>
        /// <param name="filePath">csv文件路径</param>
        /// <param name="n">表示第n行是字段title,第n+1行是记录开始</param>
        public static DataTable Csv2dt(string filePath, int n, DataTable dt)
        {
            StreamReader reader = new StreamReader(filePath, Encoding.UTF8, false);
            int i = 0, m = 0;
            reader.Peek();
            while (reader.Peek() > 0)
            {
                m = m + 1;
                string str = reader.ReadLine();
                if (m >= n + 1)
                {
                    string[] split = str.Split(',');

                    DataRow dr = dt.NewRow();
                    for (i = 0; i < split.Length; i++)
                    {
                        dr[i] = split[i];
                    }
                    dt.Rows.Add(dr);
                }
            }
            return dt;
        }
    }


}
