using System;
using System.ComponentModel;
using System.Windows.Forms;
using FlowEngineLib.Base;

namespace FlowEngineLib.Node.Algorithm;

public class FormCalibrationProperty : Form, ILoopFormProperty
{
	private DialogLoopProperty<CalibrationNodeProperty> dialog;

	private IContainer components;

	private DataGridView dataGridView1;

	private Button button_insert;

	private TextBox textBox_cali_name;

	private Label label3;

	private TextBox textBox_imag_file;

	private Label label2;

	private Button button_del;

	private Button button_save;

	private Button button_add;

	private DataGridViewTextBoxColumn no;

	private DataGridViewTextBoxColumn cmd;

	private DataGridViewTextBoxColumn Column1;

	private DataGridViewTextBoxColumn param;

	private Label label1;

	private TextBox textBox_exp_name;

	public string JsonValue { get; set; }

	public FormCalibrationProperty()
	{
		InitializeComponent();
		dialog = new DialogLoopProperty<CalibrationNodeProperty>(dataGridView1);
	}

	private void FormCalibrationProperty_Load(object sender, EventArgs e)
	{
		dialog.Load(JsonValue);
	}

	private void button_add_Click(object sender, EventArgs e)
	{
		CalibrationNodeProperty calibrationNodeProperty = new CalibrationNodeProperty();
		calibrationNodeProperty.ImgFileName = textBox_imag_file.Text;
		calibrationNodeProperty.TempName = textBox_cali_name.Text;
		calibrationNodeProperty.ExpTempName = textBox_exp_name.Text;
		dialog.Add(calibrationNodeProperty);
	}

	private void button_insert_Click(object sender, EventArgs e)
	{
		CalibrationNodeProperty calibrationNodeProperty = new CalibrationNodeProperty();
		calibrationNodeProperty.ImgFileName = textBox_imag_file.Text;
		calibrationNodeProperty.TempName = textBox_cali_name.Text;
		calibrationNodeProperty.ExpTempName = textBox_exp_name.Text;
		dialog.Insert(calibrationNodeProperty);
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
			textBox_exp_name.Text = dataGridView1.SelectedRows[0].Cells[2].Value.ToString();
			textBox_imag_file.Text = dataGridView1.SelectedRows[0].Cells[3].Value.ToString();
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
		this.dataGridView1 = new System.Windows.Forms.DataGridView();
		this.button_insert = new System.Windows.Forms.Button();
		this.textBox_cali_name = new System.Windows.Forms.TextBox();
		this.label3 = new System.Windows.Forms.Label();
		this.textBox_imag_file = new System.Windows.Forms.TextBox();
		this.label2 = new System.Windows.Forms.Label();
		this.button_del = new System.Windows.Forms.Button();
		this.button_save = new System.Windows.Forms.Button();
		this.button_add = new System.Windows.Forms.Button();
		this.label1 = new System.Windows.Forms.Label();
		this.textBox_exp_name = new System.Windows.Forms.TextBox();
		this.no = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.cmd = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.Column1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.param = new System.Windows.Forms.DataGridViewTextBoxColumn();
		((System.ComponentModel.ISupportInitialize)this.dataGridView1).BeginInit();
		base.SuspendLayout();
		this.dataGridView1.AllowUserToAddRows = false;
		this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
		this.dataGridView1.Columns.AddRange(this.no, this.cmd, this.Column1, this.param);
		this.dataGridView1.Location = new System.Drawing.Point(11, 127);
		this.dataGridView1.Name = "dataGridView1";
		this.dataGridView1.RowTemplate.Height = 23;
		this.dataGridView1.Size = new System.Drawing.Size(463, 265);
		this.dataGridView1.TabIndex = 8;
		this.dataGridView1.SelectionChanged += new System.EventHandler(dataGridView1_SelectionChanged);
		this.button_insert.Location = new System.Drawing.Point(348, 42);
		this.button_insert.Name = "button_insert";
		this.button_insert.Size = new System.Drawing.Size(75, 23);
		this.button_insert.TabIndex = 25;
		this.button_insert.Text = "插入";
		this.button_insert.UseVisualStyleBackColor = true;
		this.button_insert.Click += new System.EventHandler(button_insert_Click);
		this.textBox_cali_name.Location = new System.Drawing.Point(80, 31);
		this.textBox_cali_name.Name = "textBox_cali_name";
		this.textBox_cali_name.Size = new System.Drawing.Size(244, 21);
		this.textBox_cali_name.TabIndex = 23;
		this.textBox_cali_name.Text = "default1";
		this.label3.AutoSize = true;
		this.label3.Location = new System.Drawing.Point(15, 36);
		this.label3.Name = "label3";
		this.label3.Size = new System.Drawing.Size(53, 12);
		this.label3.TabIndex = 21;
		this.label3.Text = "校正模板";
		this.textBox_imag_file.Location = new System.Drawing.Point(80, 87);
		this.textBox_imag_file.Name = "textBox_imag_file";
		this.textBox_imag_file.Size = new System.Drawing.Size(244, 21);
		this.textBox_imag_file.TabIndex = 24;
		this.label2.AutoSize = true;
		this.label2.Location = new System.Drawing.Point(15, 92);
		this.label2.Name = "label2";
		this.label2.Size = new System.Drawing.Size(53, 12);
		this.label2.TabIndex = 22;
		this.label2.Text = "图像文件";
		this.button_del.Location = new System.Drawing.Point(348, 68);
		this.button_del.Name = "button_del";
		this.button_del.Size = new System.Drawing.Size(75, 23);
		this.button_del.TabIndex = 20;
		this.button_del.Text = "删除";
		this.button_del.UseVisualStyleBackColor = true;
		this.button_del.Click += new System.EventHandler(button_del_Click);
		this.button_save.Location = new System.Drawing.Point(348, 94);
		this.button_save.Name = "button_save";
		this.button_save.Size = new System.Drawing.Size(75, 23);
		this.button_save.TabIndex = 19;
		this.button_save.Text = "保存";
		this.button_save.UseVisualStyleBackColor = true;
		this.button_save.Click += new System.EventHandler(button_save_Click);
		this.button_add.Location = new System.Drawing.Point(348, 16);
		this.button_add.Name = "button_add";
		this.button_add.Size = new System.Drawing.Size(75, 23);
		this.button_add.TabIndex = 18;
		this.button_add.Text = "添加";
		this.button_add.UseVisualStyleBackColor = true;
		this.button_add.Click += new System.EventHandler(button_add_Click);
		this.label1.AutoSize = true;
		this.label1.Location = new System.Drawing.Point(15, 65);
		this.label1.Name = "label1";
		this.label1.Size = new System.Drawing.Size(53, 12);
		this.label1.TabIndex = 21;
		this.label1.Text = "曝光模板";
		this.textBox_exp_name.Location = new System.Drawing.Point(80, 60);
		this.textBox_exp_name.Name = "textBox_exp_name";
		this.textBox_exp_name.Size = new System.Drawing.Size(244, 21);
		this.textBox_exp_name.TabIndex = 23;
		this.textBox_exp_name.Text = "default1";
		this.no.HeaderText = "序号";
		this.no.Name = "no";
		this.no.Width = 60;
		this.cmd.HeaderText = "校正模板";
		this.cmd.Name = "cmd";
		this.cmd.Width = 80;
		this.Column1.HeaderText = "曝光模板";
		this.Column1.Name = "Column1";
		this.param.HeaderText = "图像文件";
		this.param.Name = "param";
		this.param.Width = 260;
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 12f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(482, 401);
		base.Controls.Add(this.button_insert);
		base.Controls.Add(this.textBox_exp_name);
		base.Controls.Add(this.label1);
		base.Controls.Add(this.textBox_cali_name);
		base.Controls.Add(this.label3);
		base.Controls.Add(this.textBox_imag_file);
		base.Controls.Add(this.label2);
		base.Controls.Add(this.button_del);
		base.Controls.Add(this.button_save);
		base.Controls.Add(this.button_add);
		base.Controls.Add(this.dataGridView1);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
		base.MaximizeBox = false;
		base.Name = "FormCalibrationProperty";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
		this.Text = "校正参数";
		base.Load += new System.EventHandler(FormCalibrationProperty_Load);
		((System.ComponentModel.ISupportInitialize)this.dataGridView1).EndInit();
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}
