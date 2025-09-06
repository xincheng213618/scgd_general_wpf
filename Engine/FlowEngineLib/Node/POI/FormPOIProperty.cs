using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using FlowEngineLib.Base;

namespace FlowEngineLib.Node.POI;

public class FormPOIProperty : Form, ILoopFormProperty
{
	private DialogLoopProperty<POINodeProperty> dialog;

	private IContainer components;

	private Button button_insert;

	private TextBox textBox_cali_name;

	private Label label3;

	private TextBox textBox_imag_file;

	private Label label2;

	private Button button_del;

	private Button button_save;

	private Button button_add;

	private DataGridView dataGridView1;

	private TextBox textBox_filter;

	private Label label1;

	private TextBox textBox_revise;

	private Label label4;

	private DataGridViewTextBoxColumn no;

	private DataGridViewTextBoxColumn cmd;

	private DataGridViewTextBoxColumn FilterTemp;

	private DataGridViewTextBoxColumn ReviseTemp;

	private DataGridViewTextBoxColumn OutputTemp;

	private DataGridViewTextBoxColumn param;

	private TextBox textBox_output;

	private Label label5;

	public string JsonValue { get; set; }

	public FormPOIProperty()
	{
		InitializeComponent();
		dialog = new DialogLoopProperty<POINodeProperty>(dataGridView1);
	}

	private void FormPOIProperty_Load(object sender, EventArgs e)
	{
		dialog.Load(JsonValue);
	}

	private void button_add_Click(object sender, EventArgs e)
	{
		POINodeProperty pm = BuildProperty();
		dialog.Add(pm);
	}

	private void button_insert_Click(object sender, EventArgs e)
	{
		POINodeProperty pm = BuildProperty();
		dialog.Insert(pm);
	}

	private POINodeProperty BuildProperty()
	{
		return new POINodeProperty
		{
			ImgFileName = textBox_imag_file.Text,
			TempName = textBox_cali_name.Text,
			FilterTempName = textBox_filter.Text,
			ReviseTempName = textBox_revise.Text,
			OutputTempName = textBox_output.Text
		};
	}

	private void button_del_Click(object sender, EventArgs e)
	{
		dialog.Remove();
	}

	private void button_save_Click(object sender, EventArgs e)
	{
		JsonValue = dialog.Save();
		base.DialogResult = DialogResult.OK;
	}

	private void dataGridView1_SelectionChanged(object sender, EventArgs e)
	{
		if (dataGridView1.SelectedRows.Count == 1)
		{
			textBox_cali_name.Text = dataGridView1.SelectedRows[0].Cells[1].Value.ToString();
			textBox_filter.Text = dataGridView1.SelectedRows[0].Cells[2].Value.ToString();
			textBox_revise.Text = dataGridView1.SelectedRows[0].Cells[3].Value.ToString();
			textBox_output.Text = dataGridView1.SelectedRows[0].Cells[4].Value.ToString();
			textBox_imag_file.Text = dataGridView1.SelectedRows[0].Cells[5].Value.ToString();
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
		this.button_insert = new System.Windows.Forms.Button();
		this.textBox_cali_name = new System.Windows.Forms.TextBox();
		this.label3 = new System.Windows.Forms.Label();
		this.textBox_imag_file = new System.Windows.Forms.TextBox();
		this.label2 = new System.Windows.Forms.Label();
		this.button_del = new System.Windows.Forms.Button();
		this.button_save = new System.Windows.Forms.Button();
		this.button_add = new System.Windows.Forms.Button();
		this.dataGridView1 = new System.Windows.Forms.DataGridView();
		this.textBox_filter = new System.Windows.Forms.TextBox();
		this.label1 = new System.Windows.Forms.Label();
		this.textBox_revise = new System.Windows.Forms.TextBox();
		this.label4 = new System.Windows.Forms.Label();
		this.textBox_output = new System.Windows.Forms.TextBox();
		this.label5 = new System.Windows.Forms.Label();
		this.no = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.cmd = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.FilterTemp = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.ReviseTemp = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.OutputTemp = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.param = new System.Windows.Forms.DataGridViewTextBoxColumn();
		((System.ComponentModel.ISupportInitialize)this.dataGridView1).BeginInit();
		base.SuspendLayout();
		this.button_insert.Location = new System.Drawing.Point(344, 42);
		this.button_insert.Name = "button_insert";
		this.button_insert.Size = new System.Drawing.Size(75, 23);
		this.button_insert.TabIndex = 33;
		this.button_insert.Text = "插入";
		this.button_insert.UseVisualStyleBackColor = true;
		this.button_insert.Click += new System.EventHandler(button_insert_Click);
		this.textBox_cali_name.Location = new System.Drawing.Point(76, 10);
		this.textBox_cali_name.Name = "textBox_cali_name";
		this.textBox_cali_name.Size = new System.Drawing.Size(244, 21);
		this.textBox_cali_name.TabIndex = 31;
		this.textBox_cali_name.Text = "default1";
		this.label3.AutoSize = true;
		this.label3.Location = new System.Drawing.Point(11, 15);
		this.label3.Name = "label3";
		this.label3.Size = new System.Drawing.Size(53, 12);
		this.label3.TabIndex = 29;
		this.label3.Text = "参数模板";
		this.textBox_imag_file.Location = new System.Drawing.Point(76, 38);
		this.textBox_imag_file.Name = "textBox_imag_file";
		this.textBox_imag_file.Size = new System.Drawing.Size(244, 21);
		this.textBox_imag_file.TabIndex = 32;
		this.label2.AutoSize = true;
		this.label2.Location = new System.Drawing.Point(11, 43);
		this.label2.Name = "label2";
		this.label2.Size = new System.Drawing.Size(53, 12);
		this.label2.TabIndex = 30;
		this.label2.Text = "图像文件";
		this.button_del.Location = new System.Drawing.Point(344, 68);
		this.button_del.Name = "button_del";
		this.button_del.Size = new System.Drawing.Size(75, 23);
		this.button_del.TabIndex = 28;
		this.button_del.Text = "删除";
		this.button_del.UseVisualStyleBackColor = true;
		this.button_del.Click += new System.EventHandler(button_del_Click);
		this.button_save.Location = new System.Drawing.Point(344, 94);
		this.button_save.Name = "button_save";
		this.button_save.Size = new System.Drawing.Size(75, 23);
		this.button_save.TabIndex = 27;
		this.button_save.Text = "保存";
		this.button_save.UseVisualStyleBackColor = true;
		this.button_save.Click += new System.EventHandler(button_save_Click);
		this.button_add.Location = new System.Drawing.Point(344, 16);
		this.button_add.Name = "button_add";
		this.button_add.Size = new System.Drawing.Size(75, 23);
		this.button_add.TabIndex = 26;
		this.button_add.Text = "添加";
		this.button_add.UseVisualStyleBackColor = true;
		this.button_add.Click += new System.EventHandler(button_add_Click);
		this.dataGridView1.AllowUserToAddRows = false;
		this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
		this.dataGridView1.Columns.AddRange(this.no, this.cmd, this.FilterTemp, this.ReviseTemp, this.OutputTemp, this.param);
		this.dataGridView1.Location = new System.Drawing.Point(13, 148);
		this.dataGridView1.Name = "dataGridView1";
		this.dataGridView1.RowTemplate.Height = 23;
		this.dataGridView1.Size = new System.Drawing.Size(406, 191);
		this.dataGridView1.TabIndex = 34;
		this.dataGridView1.SelectionChanged += new System.EventHandler(dataGridView1_SelectionChanged);
		this.textBox_filter.Location = new System.Drawing.Point(76, 66);
		this.textBox_filter.Name = "textBox_filter";
		this.textBox_filter.Size = new System.Drawing.Size(244, 21);
		this.textBox_filter.TabIndex = 36;
		this.label1.AutoSize = true;
		this.label1.Location = new System.Drawing.Point(11, 72);
		this.label1.Name = "label1";
		this.label1.Size = new System.Drawing.Size(53, 12);
		this.label1.TabIndex = 35;
		this.label1.Text = "过滤模板";
		this.textBox_revise.Location = new System.Drawing.Point(76, 94);
		this.textBox_revise.Name = "textBox_revise";
		this.textBox_revise.Size = new System.Drawing.Size(244, 21);
		this.textBox_revise.TabIndex = 38;
		this.label4.AutoSize = true;
		this.label4.Location = new System.Drawing.Point(11, 98);
		this.label4.Name = "label4";
		this.label4.Size = new System.Drawing.Size(53, 12);
		this.label4.TabIndex = 37;
		this.label4.Text = "修正模板";
		this.textBox_output.Location = new System.Drawing.Point(76, 121);
		this.textBox_output.Name = "textBox_output";
		this.textBox_output.Size = new System.Drawing.Size(244, 21);
		this.textBox_output.TabIndex = 40;
		this.label5.AutoSize = true;
		this.label5.Location = new System.Drawing.Point(11, 125);
		this.label5.Name = "label5";
		this.label5.Size = new System.Drawing.Size(53, 12);
		this.label5.TabIndex = 39;
		this.label5.Text = "输出模板";
		this.no.HeaderText = "序号";
		this.no.Name = "no";
		this.no.Width = 60;
		this.cmd.HeaderText = "模板";
		this.cmd.Name = "cmd";
		this.cmd.Width = 80;
		this.FilterTemp.HeaderText = "过滤模板";
		this.FilterTemp.Name = "FilterTemp";
		this.ReviseTemp.HeaderText = "修正模板";
		this.ReviseTemp.Name = "ReviseTemp";
		this.OutputTemp.HeaderText = "输出模板";
		this.OutputTemp.Name = "OutputTemp";
		this.param.HeaderText = "图像文件";
		this.param.Name = "param";
		this.param.Width = 260;
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 12f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(431, 351);
		base.Controls.Add(this.textBox_output);
		base.Controls.Add(this.label5);
		base.Controls.Add(this.textBox_revise);
		base.Controls.Add(this.label4);
		base.Controls.Add(this.textBox_filter);
		base.Controls.Add(this.label1);
		base.Controls.Add(this.dataGridView1);
		base.Controls.Add(this.button_insert);
		base.Controls.Add(this.textBox_cali_name);
		base.Controls.Add(this.label3);
		base.Controls.Add(this.textBox_imag_file);
		base.Controls.Add(this.label2);
		base.Controls.Add(this.button_del);
		base.Controls.Add(this.button_save);
		base.Controls.Add(this.button_add);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
		base.MaximizeBox = false;
		base.Name = "FormPOIProperty";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
		this.Text = "关注点参数";
		base.Load += new System.EventHandler(FormPOIProperty_Load);
		((System.ComponentModel.ISupportInitialize)this.dataGridView1).EndInit();
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}
