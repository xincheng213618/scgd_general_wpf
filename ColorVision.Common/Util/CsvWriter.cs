#pragma warning disable CS8602,CA1806,CA2201
using System.Collections.Generic;
using System;
using System.Data;
using System.IO;
using System.Text;

namespace ColorVision.Util
{
    public class CsvWriter
    {

        public static void WriteToCsv<T>(T data, string filePath)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            // 创建StringBuilder用于写入CSV数据
            var csvBuilder = new StringBuilder();

            // 获取类型的属性，这些属性将成为CSV的列头
            var properties = typeof(T).GetProperties();

            // 写入列头
            for (int i = 0; i < properties.Length; i++)
            {
                // 添加列名
                csvBuilder.Append(properties[i].Name);

                // 如果不是最后一列，则添加逗号
                if (i < properties.Length - 1)
                    csvBuilder.Append(',');
            }

            // 添加换行符
            csvBuilder.AppendLine();

            for (int i = 0; i < properties.Length; i++)
            {
                // 获取属性值
                var value = properties[i].GetValue(data, null);

                // 处理可能包含逗号或引号的值
                string valueAsString = value?.ToString() ?? string.Empty;
                if (valueAsString.Contains(',') || valueAsString.Contains("\""))
                {
                    valueAsString = $"\"{valueAsString.Replace("\"", "\"\"")}\"";
                }

                // 添加值
                csvBuilder.Append(valueAsString);

                // 如果不是最后一列，则添加逗号
                if (i < properties.Length - 1)
                    csvBuilder.Append(',');
            }

            // 添加换行符
            csvBuilder.AppendLine();

            // 将生成的CSV内容写入文件
            File.WriteAllText(filePath, csvBuilder.ToString());
        }




        public static void WriteToCsv<T>(IEnumerable<T> data, string filePath)
        {
            // 确保有数据要写入
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            // 创建StringBuilder用于写入CSV数据
            var csvBuilder = new StringBuilder();

            // 获取类型的属性，这些属性将成为CSV的列头
            var properties = typeof(T).GetProperties();

            // 写入列头
            for (int i = 0; i < properties.Length; i++)
            {
                // 添加列名
                csvBuilder.Append(properties[i].Name);

                // 如果不是最后一列，则添加逗号
                if (i < properties.Length - 1)
                    csvBuilder.Append(',');
            }

            // 添加换行符
            csvBuilder.AppendLine();

            // 写入数据行
            foreach (var item in data)
            {
                for (int i = 0; i < properties.Length; i++)
                {
                    // 获取属性值
                    var value = properties[i].GetValue(item, null);

                    // 处理可能包含逗号或引号的值
                    string valueAsString = value?.ToString() ?? string.Empty;
                    if (valueAsString.Contains(',') || valueAsString.Contains("\""))
                    {
                        valueAsString = $"\"{valueAsString.Replace("\"", "\"\"")}\"";
                    }

                    // 添加值
                    csvBuilder.Append(valueAsString);

                    // 如果不是最后一列，则添加逗号
                    if (i < properties.Length - 1)
                        csvBuilder.Append(',');
                }

                // 添加换行符
                csvBuilder.AppendLine();
            }

            // 将生成的CSV内容写入文件
            File.WriteAllText(filePath, csvBuilder.ToString());
        }


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
