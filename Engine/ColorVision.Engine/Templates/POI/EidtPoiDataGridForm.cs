#pragma warning disable CS8601,CS8622,CA1822,CS8602
using ColorVision.Common.Collections;
using ColorVision.ImageEditor.Draw;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows.Forms;

namespace ColorVision.Engine.Templates.POI
{
    public partial class EidtPoiDataGridForm : Form
    {
        public ObservableCollection<IDrawingVisual> DrawingVisualLists { get; set; } = new BulkObservableCollection<IDrawingVisual>();

        public EidtPoiDataGridForm(ObservableCollection<IDrawingVisual> drawingVisualLists)
        {
            DrawingVisualLists = drawingVisualLists;
            InitializeComponent();

            DataTable table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("HaloMOVE", typeof(int));
            table.Columns.Add("HaloThreadV", typeof(int));
            table.Columns.Add("HaloSize", typeof(int));
            table.Columns.Add("KeyMOVE", typeof(int));
            table.Columns.Add("KeyThreadV", typeof(int));
            table.Columns.Add("HaloScale", typeof(double));
            table.Columns.Add("KeyScale", typeof(double));
            table.Columns.Add("HaloOffsetX", typeof(int));
            table.Columns.Add("HaloOffsetY", typeof(int));
            table.Columns.Add("KeyOffsetX", typeof(int));
            table.Columns.Add("KeyOffsetY", typeof(int));
            table.Columns.Add("Area", typeof(double));
            
            foreach (var item in DrawingVisualLists)
            {
                var row = table.NewRow();
                row["Id"] = item.BaseAttribute?.Id ?? null;
                if (item.BaseAttribute is ITextProperties text)
                {
                    row["Name"] = text.Text;
                }
                if (item.BaseAttribute.Param is KBPoiVMParam param)
                {
                    row["Area"] = param.Area;
                    row["HaloScale"] = param.HaloScale;
                    row["HaloMOVE"] = param.HaloOutMOVE;
                    row["HaloOffsetX"] = param.HaloOffsetX;
                    row["HaloOffsetY"] = param.HaloOffsetY;
                    row["HaloSize"] = param.HaloSize;
                    row["KeyScale"] = param.KeyScale;
                    row["KeyOffsetX"] = param.KeyOffsetX;
                    row["KeyOffsetY"] = param.KeyOffsetY;
                    row["KeyMOVE"] = param.KeyOutMOVE;
                    row["KeyThreadV"] = param.KeyThreadV;
                    row["HaloThreadV"] = param.HaloThreadV;
                }
                table.Rows.Add(row);
            }

            dataGridView1.DataSource = table;
            dataGridView1.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithAutoHeaderText;
            dataGridView1.KeyDown += DataGridView1_KeyDown;
            dataGridView1.Dock = DockStyle.Fill;
            this.Closed += (s,e) => UpdateDrawingVisualListsFromDataTable((DataTable)dataGridView1.DataSource);
        }
        private static readonly string[] separator = new[] { "\r\n", "\r", "\n" };

        private void CreateOrUpdateDrawingVisual(DataRow row, IDrawingVisual drawingVisual)
        {
            drawingVisual.BaseAttribute.Id = row.Field<int>("Id");
            drawingVisual.BaseAttribute.Name = row.Field<string>("Name");
            if (drawingVisual.BaseAttribute is ITextProperties text)
            {
                text.Text = row.Field<string>("Name");
            }
            if (drawingVisual.BaseAttribute.Param is KBPoiVMParam param)
            {
                param.Area = row.Field<double>("Area");
                param.HaloScale = row.Field<double>("HaloScale");
                param.HaloOutMOVE = row.Field<int>("HaloMOVE");
                param.HaloOffsetX = row.Field<int>("HaloOffsetX");
                param.HaloOffsetY = row.Field<int>("HaloOffsetY");
                param.HaloSize = row.Field<int>("HaloSize");
                param.KeyScale = row.Field<double>("KeyScale");
                param.KeyOffsetX = row.Field<int>("KeyOffsetX");
                param.KeyOffsetY = row.Field<int>("KeyOffsetY");
                param.KeyOutMOVE = row.Field<int>("KeyMOVE");
                param.KeyThreadV = row.Field<int>("KeyThreadV");
                param.HaloThreadV = row.Field<int>("HaloThreadV");
            }
        }

        private void UpdateDrawingVisualListsFromDataTable(DataTable table)
        {
            int i = 0;
            foreach (DataRow row in table.Rows)
            {
                if (i< DrawingVisualLists.Count)
                {
                    CreateOrUpdateDrawingVisual(row, DrawingVisualLists[i]);
                }
                i++;
            }
        }


        private void DataGridView1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.V)
            {
                PasteFromClipboardAtCurrentPosition();
                e.Handled = true;
            }
        }
        private void PasteFromClipboardAtCurrentPosition()
        {
            if (dataGridView1.CurrentCell == null)
                return;

            int startRow = dataGridView1.CurrentCell.RowIndex;
            int startCol = dataGridView1.CurrentCell.ColumnIndex;

            string clipboardText = Clipboard.GetText();
            string[] lines = clipboardText.Split(separator, StringSplitOptions.None);

            DataTable table = (DataTable)dataGridView1.DataSource;

            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                string[] values = lines[i].Split('\t');

                int rowIndex = startRow + i;
                if (rowIndex >= table.Rows.Count)
                    table.Rows.Add(table.NewRow());

                for (int j = 0; j < values.Length; j++)
                {
                    int colIndex = startCol + j;
                    if (colIndex < table.Columns.Count)
                    {
                        table.Rows[rowIndex][colIndex] = values[j];
                    }
                }
            }
        }
    }
}
