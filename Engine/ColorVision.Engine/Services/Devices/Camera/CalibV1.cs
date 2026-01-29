using cvColorVision;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;

namespace WindowsFormsTest
{
    [JsonObject(MemberSerialization.OptIn)]
    public partial class CalibV1 : Form
    {
        public string strLoadname = "cfg//CalibV1.cfg";

        [JsonProperty]
        public List<CalibrationItem> listItem = new List<CalibrationItem>();

        public List<CalibrationItem> listSYS = new List<CalibrationItem>();
        private BindingList<CalibrationItem> calibrationItems;

        public CalibV1()
        {
            if (File.Exists(strLoadname))
            {
                string json = System.IO.File.ReadAllText(strLoadname);
                JsonConvert.PopulateObject(json, this);
            }

            InitializeComponent();
        }

        private void CalibrationSysSetup_Load(object sender, EventArgs e)
        {
            List<string> listtitle = new List<string>();

            for (int i = 0; i < listSYS.Count; i++)
            {
                listtitle.Add(listSYS[i].title);
            }

            ListPointsHeader(listtitle);

            List<CalibrationItem> newList = new List<CalibrationItem>(listItem);

            foreach (var item in newList)
            {
                if (!listtitle.Contains(item.title))
                {
                    listItem.Remove(item);
                }
            }

            calibrationItems = new BindingList<CalibrationItem>(listItem);

            dataGridView1.DataSource = calibrationItems;
            dataGridView1.AutoGenerateColumns = true;
        }

        private void btn_apply_Click(object sender, EventArgs e)
        {
            CommonUtil.SaveCfgFile<CalibV1>(strLoadname, this);
            this.DialogResult = DialogResult.OK;
        }

        private void ListPointsHeader(List<string> listtitle)
        {
           dataGridView1.Columns.Clear();
           DataGridViewComboBoxColumn cbxCol = new DataGridViewComboBoxColumn
            {
                DataSource = new List<string>(listtitle),
                Name = "title",
                DataPropertyName = "title",
                HeaderText = "校正标题",
                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox
           };

            cbxCol.Width = 200;
            dataGridView1.Columns.Add(cbxCol);

            DataGridViewComboBoxColumn cbxCol1 = new DataGridViewComboBoxColumn
            {
                DataSource = Enum.GetValues(typeof(CalibrationType)),
                Name = "type",
                DataPropertyName = "type",
                HeaderText = "校正类型",
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton
            };

            cbxCol1.Width = 120;
            dataGridView1.Columns.Add(cbxCol1);

            DataGridViewCheckBoxColumn CheckBoxCol = new DataGridViewCheckBoxColumn();
            CheckBoxCol.HeaderText = "启用";
            CheckBoxCol.Width = 85;
            CheckBoxCol.Name = "enable";
            CheckBoxCol.DataPropertyName = "enable";
            dataGridView1.Columns.Add(CheckBoxCol);

            DataGridViewTextBoxColumn TextCol = new DataGridViewTextBoxColumn();
            TextCol.HeaderText = "文件名";
            TextCol.Width = 120;
            TextCol.ReadOnly = true;
            TextCol.Name = "doc";
            TextCol.DataPropertyName = "doc";
            TextCol.Visible = false;

            dataGridView1.Columns.Add(TextCol);

            DataGridViewButtonColumn ButtonCol = new DataGridViewButtonColumn();
            ButtonCol.HeaderText = "操作";
            ButtonCol.UseColumnTextForButtonValue = true;
            ButtonCol.Width = 100;
            ButtonCol.Name = "Delete";
            ButtonCol.Text = "删除";

            dataGridView1.Columns.Add(ButtonCol);
        }

        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 0)
            {
                DataGridViewCell cell = dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex];

                if (cell.FormattedValue.ToString() == "删除")
                {
                    calibrationItems.RemoveAt(e.RowIndex);
                }
            }
        }
    }
}
