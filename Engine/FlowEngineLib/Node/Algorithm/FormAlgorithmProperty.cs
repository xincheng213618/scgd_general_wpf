using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using FlowEngineLib.Base;

namespace FlowEngineLib.Node.Algorithm;

public class FormAlgorithmProperty : Form, ILoopFormProperty
{
	private DialogLoopProperty<AlgorithmNodeProperty> dialog;

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

	private Label label1;

	private ComboBox comboBox1;

	private Label label_poi;

	private TextBox textBox_poi;

	private Label label5;

	private DataGridViewTextBoxColumn no;

	private DataGridViewTextBoxColumn Column1;

	private DataGridViewTextBoxColumn cmd;

	private DataGridViewTextBoxColumn param;

	private DataGridViewTextBoxColumn Column2;

	public string JsonValue { get; set; }

	public FormAlgorithmProperty()
	{
		InitializeComponent();
		dialog = new DialogLoopProperty<AlgorithmNodeProperty>(dataGridView1);
	}

	private void FormAlgorithmProperty_Load(object sender, EventArgs e)
	{
		dialog.Load(JsonValue);
		comboBox1.SelectedIndex = 0;
	}

	private void button_add_Click(object sender, EventArgs e)
	{
		AlgorithmNodeProperty algorithmNodeProperty = new AlgorithmNodeProperty();
		algorithmNodeProperty.AlgorithmType = comboBox1.Text;
		algorithmNodeProperty.ImgFileName = textBox_imag_file.Text;
		algorithmNodeProperty.TempName = textBox_cali_name.Text;
		if (textBox_poi.Visible)
		{
			algorithmNodeProperty.POIName = textBox_poi.Text;
		}
		dialog.Add(algorithmNodeProperty);
	}

	private void button_insert_Click(object sender, EventArgs e)
	{
		AlgorithmNodeProperty algorithmNodeProperty = new AlgorithmNodeProperty();
		algorithmNodeProperty.AlgorithmType = comboBox1.Text;
		algorithmNodeProperty.ImgFileName = textBox_imag_file.Text;
		algorithmNodeProperty.TempName = textBox_cali_name.Text;
		if (textBox_poi.Visible)
		{
			algorithmNodeProperty.POIName = textBox_poi.Text;
		}
		dialog.Insert(algorithmNodeProperty);
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
			comboBox1.Text = dataGridView1.SelectedRows[0].Cells[1].Value.ToString();
			textBox_cali_name.Text = dataGridView1.SelectedRows[0].Cells[2].Value.ToString();
			textBox_imag_file.Text = dataGridView1.SelectedRows[0].Cells[3].Value.ToString();
			textBox_poi.Text = dataGridView1.SelectedRows[0].Cells[4]?.Value?.ToString();
		}
	}

	private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
	{
		if (comboBox1.SelectedIndex == 0)
		{
			EnablePOI(enable: true);
		}
		else
		{
			EnablePOI(enable: false);
		}
	}

	private void EnablePOI(bool enable)
	{
		label_poi.Visible = enable;
		label5.Visible = enable;
		textBox_poi.Visible = enable;
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
		this.no = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.Column1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.cmd = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.param = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.Column2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.button_insert = new System.Windows.Forms.Button();
		this.textBox_cali_name = new System.Windows.Forms.TextBox();
		this.label3 = new System.Windows.Forms.Label();
		this.textBox_imag_file = new System.Windows.Forms.TextBox();
		this.label2 = new System.Windows.Forms.Label();
		this.button_del = new System.Windows.Forms.Button();
		this.button_save = new System.Windows.Forms.Button();
		this.button_add = new System.Windows.Forms.Button();
		this.label1 = new System.Windows.Forms.Label();
		this.comboBox1 = new System.Windows.Forms.ComboBox();
		this.label_poi = new System.Windows.Forms.Label();
		this.textBox_poi = new System.Windows.Forms.TextBox();
		this.label5 = new System.Windows.Forms.Label();
		((System.ComponentModel.ISupportInitialize)this.dataGridView1).BeginInit();
		base.SuspendLayout();
		this.dataGridView1.AllowUserToAddRows = false;
		this.dataGridView1.AllowUserToDeleteRows = false;
		this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
		this.dataGridView1.Columns.AddRange(this.no, this.Column1, this.cmd, this.param, this.Column2);
		this.dataGridView1.Location = new System.Drawing.Point(12, 149);
		this.dataGridView1.Name = "dataGridView1";
		this.dataGridView1.RowTemplate.Height = 23;
		this.dataGridView1.Size = new System.Drawing.Size(512, 252);
		this.dataGridView1.TabIndex = 9;
		this.dataGridView1.SelectionChanged += new System.EventHandler(dataGridView1_SelectionChanged);
		this.no.HeaderText = "序号";
		this.no.Name = "no";
		this.no.Width = 60;
		this.Column1.HeaderText = "算法";
		this.Column1.Name = "Column1";
		this.cmd.HeaderText = "模板";
		this.cmd.Name = "cmd";
		this.cmd.Width = 80;
		this.param.HeaderText = "图像文件";
		this.param.Name = "param";
		this.param.Width = 260;
		this.Column2.HeaderText = "POI";
		this.Column2.Name = "Column2";
		this.button_insert.Location = new System.Drawing.Point(390, 41);
		this.button_insert.Name = "button_insert";
		this.button_insert.Size = new System.Drawing.Size(75, 23);
		this.button_insert.TabIndex = 33;
		this.button_insert.Text = "插入";
		this.button_insert.UseVisualStyleBackColor = true;
		this.button_insert.Click += new System.EventHandler(button_insert_Click);
		this.textBox_cali_name.Location = new System.Drawing.Point(102, 38);
		this.textBox_cali_name.Name = "textBox_cali_name";
		this.textBox_cali_name.Size = new System.Drawing.Size(244, 21);
		this.textBox_cali_name.TabIndex = 31;
		this.textBox_cali_name.Text = "default1";
		this.label3.AutoSize = true;
		this.label3.Location = new System.Drawing.Point(37, 46);
		this.label3.Name = "label3";
		this.label3.Size = new System.Drawing.Size(53, 12);
		this.label3.TabIndex = 29;
		this.label3.Text = "参数模板";
		this.textBox_imag_file.Location = new System.Drawing.Point(102, 68);
		this.textBox_imag_file.Name = "textBox_imag_file";
		this.textBox_imag_file.Size = new System.Drawing.Size(244, 21);
		this.textBox_imag_file.TabIndex = 32;
		this.label2.AutoSize = true;
		this.label2.Location = new System.Drawing.Point(37, 75);
		this.label2.Name = "label2";
		this.label2.Size = new System.Drawing.Size(53, 12);
		this.label2.TabIndex = 30;
		this.label2.Text = "图像文件";
		this.button_del.Location = new System.Drawing.Point(390, 67);
		this.button_del.Name = "button_del";
		this.button_del.Size = new System.Drawing.Size(75, 23);
		this.button_del.TabIndex = 28;
		this.button_del.Text = "删除";
		this.button_del.UseVisualStyleBackColor = true;
		this.button_del.Click += new System.EventHandler(button_del_Click);
		this.button_save.Location = new System.Drawing.Point(390, 93);
		this.button_save.Name = "button_save";
		this.button_save.Size = new System.Drawing.Size(75, 23);
		this.button_save.TabIndex = 27;
		this.button_save.Text = "保存";
		this.button_save.UseVisualStyleBackColor = true;
		this.button_save.Click += new System.EventHandler(button_save_Click);
		this.button_add.Location = new System.Drawing.Point(390, 15);
		this.button_add.Name = "button_add";
		this.button_add.Size = new System.Drawing.Size(75, 23);
		this.button_add.TabIndex = 26;
		this.button_add.Text = "添加";
		this.button_add.UseVisualStyleBackColor = true;
		this.button_add.Click += new System.EventHandler(button_add_Click);
		this.label1.AutoSize = true;
		this.label1.Location = new System.Drawing.Point(37, 17);
		this.label1.Name = "label1";
		this.label1.Size = new System.Drawing.Size(53, 12);
		this.label1.TabIndex = 29;
		this.label1.Text = "算法名称";
		this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
		this.comboBox1.FormattingEnabled = true;
		this.comboBox1.Items.AddRange(new object[5] { "MTF", "SFR", "FOV", "鬼影", "畸变" });
		this.comboBox1.Location = new System.Drawing.Point(102, 9);
		this.comboBox1.Name = "comboBox1";
		this.comboBox1.Size = new System.Drawing.Size(244, 20);
		this.comboBox1.TabIndex = 34;
		this.comboBox1.SelectedIndexChanged += new System.EventHandler(comboBox1_SelectedIndexChanged);
		this.label_poi.AutoSize = true;
		this.label_poi.Location = new System.Drawing.Point(37, 104);
		this.label_poi.Name = "label_poi";
		this.label_poi.Size = new System.Drawing.Size(47, 12);
		this.label_poi.TabIndex = 29;
		this.label_poi.Text = "POI模板";
		this.textBox_poi.Location = new System.Drawing.Point(102, 98);
		this.textBox_poi.Name = "textBox_poi";
		this.textBox_poi.Size = new System.Drawing.Size(244, 21);
		this.textBox_poi.TabIndex = 31;
		this.textBox_poi.Text = "POI.Default";
		this.label5.AutoSize = true;
		this.label5.Font = new System.Drawing.Font("宋体", 9f, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 134);
		this.label5.ForeColor = System.Drawing.Color.Red;
		this.label5.Location = new System.Drawing.Point(37, 126);
		this.label5.Name = "label5";
		this.label5.Size = new System.Drawing.Size(180, 12);
		this.label5.TabIndex = 29;
		this.label5.Text = "* POI模板仅在MTF/LED下设置";
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 12f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(536, 413);
		base.Controls.Add(this.comboBox1);
		base.Controls.Add(this.button_insert);
		base.Controls.Add(this.textBox_poi);
		base.Controls.Add(this.textBox_cali_name);
		base.Controls.Add(this.label5);
		base.Controls.Add(this.label_poi);
		base.Controls.Add(this.label1);
		base.Controls.Add(this.label3);
		base.Controls.Add(this.textBox_imag_file);
		base.Controls.Add(this.label2);
		base.Controls.Add(this.button_del);
		base.Controls.Add(this.button_save);
		base.Controls.Add(this.button_add);
		base.Controls.Add(this.dataGridView1);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
		base.MaximizeBox = false;
		base.Name = "FormAlgorithmProperty";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
		this.Text = "算法参数";
		base.Load += new System.EventHandler(FormAlgorithmProperty_Load);
		((System.ComponentModel.ISupportInitialize)this.dataGridView1).EndInit();
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}
