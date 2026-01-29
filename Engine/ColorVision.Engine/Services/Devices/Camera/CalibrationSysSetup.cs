using cvColorVision;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;

namespace WindowsFormsTest
{
    public partial class CalibrationSysSetup : Form
    {
        public List<CalibrationItem> listItem = new List<CalibrationItem>();
        private BindingList<CalibrationItem> calibrationItems;
        public CalibrationSysSetup(List<CalibrationItem> items)
        {
            InitializeComponent();
            if (items == null)
                items = new List<CalibrationItem>();
            listItem = items;
        }

        private void CalibrationSysSetup_Load(object sender, EventArgs e)
        {
            ListPointsHeader();
            calibrationItems = new BindingList<CalibrationItem>(listItem);

            dataGridView1.DataSource = calibrationItems;
            dataGridView1.AutoGenerateColumns = true;
        }

        private void btn_del_Click(object sender, EventArgs e)
        {

        }

        private void btn_add_Click(object sender, EventArgs e)
        {

        }

        private void btn_apply_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
        }

        private void ListPointsHeader()
        {
            DataGridViewTextBoxColumn TextCol = new DataGridViewTextBoxColumn();
            TextCol.HeaderText = "校正标题";
            TextCol.Name = "title";
            TextCol.Width = 200;
            TextCol.DataPropertyName = "title";
            dataGridView1.Columns.Add(TextCol);

            DataGridViewComboBoxColumn cbxCol = new DataGridViewComboBoxColumn
            {
                DataSource = Enum.GetValues(typeof(CalibrationType)),
                Name = "type",
                DataPropertyName = "type",
                HeaderText = "校正类型",
                DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton
            };

            cbxCol.Width = 100;
            dataGridView1.Columns.Add(cbxCol);

            DataGridViewCheckBoxColumn CheckBoxCol = new DataGridViewCheckBoxColumn();
            CheckBoxCol.HeaderText = "启用";
            CheckBoxCol.Width = 65;
            CheckBoxCol.Name = "enable";
            CheckBoxCol.DataPropertyName = "enable";
            dataGridView1.Columns.Add(CheckBoxCol);

            TextCol = new DataGridViewTextBoxColumn();
            TextCol.HeaderText = "文件名";
            TextCol.Width = 450;
            TextCol.ReadOnly = true;
            TextCol.Name = "doc";
            TextCol.DataPropertyName = "doc";

            dataGridView1.Columns.Add(TextCol);

            DataGridViewButtonColumn ButtonCol = new DataGridViewButtonColumn();
            ButtonCol.HeaderText = "操作";
            ButtonCol.UseColumnTextForButtonValue = true;
            ButtonCol.Width = 95;
            ButtonCol.Name = "Delete";
            ButtonCol.Text = "删除";

            dataGridView1.Columns.Add(ButtonCol);
        }

        private void dataGridView1_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 4)
            {
                DataGridViewCell clickedCell = dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex];
                DataGridViewCell clickedCelltitle = dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex - 3];

                string apppath = Environment.CurrentDirectory.ToString();
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.InitialDirectory = apppath;
                openFileDialog.Filter = "DAT|*.dat||";
                openFileDialog.RestoreDirectory = true;
                openFileDialog.FilterIndex = 1;
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string fName;
                    fName = openFileDialog.FileName;
                    if (!fName.Contains(apppath))
                    {
                        MessageBox.Show("请选择软件根目录文件！");
                        return;
                    }
                    int fileNameLength = fName.LastIndexOf('\\') + 1;
                    string fileName = fName.Substring(fileNameLength, fName.Length - fileNameLength);
                    int pathLength = fName.IndexOf(apppath) + apppath.Length + 1;
                    string pathName = fName.Substring(pathLength, fName.Length - pathLength);
                    clickedCell.Value = pathName;
                    clickedCelltitle.Value = pathName;
                }
            }

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
