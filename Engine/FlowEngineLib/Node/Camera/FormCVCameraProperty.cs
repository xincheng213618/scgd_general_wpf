using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using FlowEngineLib.Base;

namespace FlowEngineLib.Node.Camera;

public class FormCVCameraProperty : Form, ILoopFormProperty
{
	private DialogLoopProperty<CVCameraNodeProperty> dialog;

	private IContainer components;

	private Button button_del;

	private Button button_save;

	private DataGridView dataGridView1;

	private Button button_insert;

	private TextBox textBox_cali_name;

	private Label label3;

	private TextBox textBox_exp_time_r;

	private Label label2;

	private Button button_add;

	private Label label1;

	private TextBox textBox_exp_time_g;

	private Label label4;

	private TextBox textBox_exp_time_b;

	private TextBox textBox_poi;

	private Label label5;

	private TextBox textBox_poi_filter;

	private Label label6;

	private TextBox textBox_poi_revise;

	private Label label7;

	private TextBox textBox_avg_count;

	private Label label8;

	private TextBox textBox_gain;

	private Label label9;

	private DataGridViewTextBoxColumn no;

	private DataGridViewTextBoxColumn avgCount;

	private DataGridViewTextBoxColumn Column_gain;

	private DataGridViewTextBoxColumn cmd;

	private DataGridViewTextBoxColumn Column1;

	private DataGridViewTextBoxColumn Column2;

	private DataGridViewTextBoxColumn param;

	private DataGridViewTextBoxColumn POI;

	private DataGridViewTextBoxColumn POIFilter;

	public string JsonValue { get; set; }

	public FormCVCameraProperty()
	{
		InitializeComponent();
		dialog = new DialogLoopProperty<CVCameraNodeProperty>(dataGridView1);
	}

	private void FormCVCameraProperty_Load(object sender, EventArgs e)
	{
		dialog.Load(JsonValue);
	}

	private void button_add_Click(object sender, EventArgs e)
	{
		CVCameraNodeProperty pm = BuildProperty();
		dialog.Add(pm);
	}

	private void button_insert_Click(object sender, EventArgs e)
	{
		CVCameraNodeProperty pm = BuildProperty();
		dialog.Insert(pm);
	}

	private CVCameraNodeProperty BuildProperty()
	{
		return new CVCameraNodeProperty
		{
			AvgCount = textBox_avg_count.Text,
			Gain = textBox_gain.Text,
			TempR = textBox_exp_time_r.Text,
			TempG = textBox_exp_time_g.Text,
			TempB = textBox_exp_time_b.Text,
			CaliTempName = textBox_cali_name.Text,
			POITempName = textBox_poi.Text,
			POIFilterTempName = textBox_poi_filter.Text,
			POIReviseTempName = textBox_poi_revise.Text
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
			textBox_avg_count.Text = dataGridView1.SelectedRows[0].Cells[1].Value.ToString();
			textBox_gain.Text = dataGridView1.SelectedRows[0].Cells[2].Value.ToString();
			textBox_exp_time_r.Text = dataGridView1.SelectedRows[0].Cells[3].Value.ToString();
			textBox_exp_time_g.Text = dataGridView1.SelectedRows[0].Cells[4].Value.ToString();
			textBox_exp_time_b.Text = dataGridView1.SelectedRows[0].Cells[5].Value.ToString();
			textBox_cali_name.Text = dataGridView1.SelectedRows[0].Cells[6].Value.ToString();
			textBox_poi.Text = dataGridView1.SelectedRows[0].Cells[7].Value.ToString();
			textBox_poi_filter.Text = dataGridView1.SelectedRows[0].Cells[8].Value.ToString();
			textBox_poi_revise.Text = dataGridView1.SelectedRows[0].Cells[9].Value.ToString();
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
		this.button_del = new System.Windows.Forms.Button();
		this.button_save = new System.Windows.Forms.Button();
		this.dataGridView1 = new System.Windows.Forms.DataGridView();
		this.no = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.avgCount = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.Column_gain = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.cmd = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.Column1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.Column2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.param = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.POI = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.POIFilter = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.button_insert = new System.Windows.Forms.Button();
		this.textBox_cali_name = new System.Windows.Forms.TextBox();
		this.label3 = new System.Windows.Forms.Label();
		this.textBox_exp_time_r = new System.Windows.Forms.TextBox();
		this.label2 = new System.Windows.Forms.Label();
		this.button_add = new System.Windows.Forms.Button();
		this.label1 = new System.Windows.Forms.Label();
		this.textBox_exp_time_g = new System.Windows.Forms.TextBox();
		this.label4 = new System.Windows.Forms.Label();
		this.textBox_exp_time_b = new System.Windows.Forms.TextBox();
		this.textBox_poi = new System.Windows.Forms.TextBox();
		this.label5 = new System.Windows.Forms.Label();
		this.textBox_poi_filter = new System.Windows.Forms.TextBox();
		this.label6 = new System.Windows.Forms.Label();
		this.textBox_poi_revise = new System.Windows.Forms.TextBox();
		this.label7 = new System.Windows.Forms.Label();
		this.textBox_avg_count = new System.Windows.Forms.TextBox();
		this.label8 = new System.Windows.Forms.Label();
		this.textBox_gain = new System.Windows.Forms.TextBox();
		this.label9 = new System.Windows.Forms.Label();
		((System.ComponentModel.ISupportInitialize)this.dataGridView1).BeginInit();
		base.SuspendLayout();
		this.button_del.Location = new System.Drawing.Point(289, 63);
		this.button_del.Name = "button_del";
		this.button_del.Size = new System.Drawing.Size(75, 23);
		this.button_del.TabIndex = 21;
		this.button_del.Text = "删除";
		this.button_del.UseVisualStyleBackColor = true;
		this.button_del.Click += new System.EventHandler(button_del_Click);
		this.button_save.Location = new System.Drawing.Point(289, 89);
		this.button_save.Name = "button_save";
		this.button_save.Size = new System.Drawing.Size(75, 23);
		this.button_save.TabIndex = 20;
		this.button_save.Text = "保存";
		this.button_save.UseVisualStyleBackColor = true;
		this.button_save.Click += new System.EventHandler(button_save_Click);
		this.dataGridView1.AllowUserToAddRows = false;
		this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
		this.dataGridView1.Columns.AddRange(this.no, this.avgCount, this.Column_gain, this.cmd, this.Column1, this.Column2, this.param, this.POI, this.POIFilter);
		this.dataGridView1.Location = new System.Drawing.Point(12, 244);
		this.dataGridView1.Name = "dataGridView1";
		this.dataGridView1.RowTemplate.Height = 23;
		this.dataGridView1.Size = new System.Drawing.Size(377, 237);
		this.dataGridView1.TabIndex = 19;
		this.dataGridView1.SelectionChanged += new System.EventHandler(dataGridView1_SelectionChanged);
		this.no.HeaderText = "序号";
		this.no.Name = "no";
		this.no.Width = 55;
		this.avgCount.HeaderText = "平均次数";
		this.avgCount.Name = "avgCount";
		this.Column_gain.HeaderText = "增益";
		this.Column_gain.Name = "Column_gain";
		this.cmd.HeaderText = "R";
		this.cmd.Name = "cmd";
		this.cmd.Width = 70;
		this.Column1.HeaderText = "G";
		this.Column1.Name = "Column1";
		this.Column1.Width = 70;
		this.Column2.HeaderText = "B";
		this.Column2.Name = "Column2";
		this.Column2.Width = 70;
		this.param.HeaderText = "校正模板";
		this.param.Name = "param";
		this.param.Width = 150;
		this.POI.HeaderText = "POI";
		this.POI.Name = "POI";
		this.POIFilter.HeaderText = "POI过滤";
		this.POIFilter.Name = "POIFilter";
		this.button_insert.Location = new System.Drawing.Point(289, 37);
		this.button_insert.Name = "button_insert";
		this.button_insert.Size = new System.Drawing.Size(75, 23);
		this.button_insert.TabIndex = 26;
		this.button_insert.Text = "插入";
		this.button_insert.UseVisualStyleBackColor = true;
		this.button_insert.Click += new System.EventHandler(button_insert_Click);
		this.textBox_cali_name.Location = new System.Drawing.Point(84, 140);
		this.textBox_cali_name.Name = "textBox_cali_name";
		this.textBox_cali_name.Size = new System.Drawing.Size(156, 21);
		this.textBox_cali_name.TabIndex = 24;
		this.textBox_cali_name.Text = "default1";
		this.label3.AutoSize = true;
		this.label3.Location = new System.Drawing.Point(19, 145);
		this.label3.Name = "label3";
		this.label3.Size = new System.Drawing.Size(53, 12);
		this.label3.TabIndex = 22;
		this.label3.Text = "校正模板";
		this.textBox_exp_time_r.Location = new System.Drawing.Point(84, 68);
		this.textBox_exp_time_r.Name = "textBox_exp_time_r";
		this.textBox_exp_time_r.Size = new System.Drawing.Size(156, 21);
		this.textBox_exp_time_r.TabIndex = 25;
		this.textBox_exp_time_r.Text = "100";
		this.label2.AutoSize = true;
		this.label2.Location = new System.Drawing.Point(19, 73);
		this.label2.Name = "label2";
		this.label2.Size = new System.Drawing.Size(65, 12);
		this.label2.TabIndex = 23;
		this.label2.Text = "R 曝光(ms)";
		this.button_add.Location = new System.Drawing.Point(289, 11);
		this.button_add.Name = "button_add";
		this.button_add.Size = new System.Drawing.Size(75, 23);
		this.button_add.TabIndex = 18;
		this.button_add.Text = "添加";
		this.button_add.UseVisualStyleBackColor = true;
		this.button_add.Click += new System.EventHandler(button_add_Click);
		this.label1.AutoSize = true;
		this.label1.Location = new System.Drawing.Point(19, 97);
		this.label1.Name = "label1";
		this.label1.Size = new System.Drawing.Size(65, 12);
		this.label1.TabIndex = 23;
		this.label1.Text = "G 曝光(ms)";
		this.textBox_exp_time_g.Location = new System.Drawing.Point(84, 92);
		this.textBox_exp_time_g.Name = "textBox_exp_time_g";
		this.textBox_exp_time_g.Size = new System.Drawing.Size(156, 21);
		this.textBox_exp_time_g.TabIndex = 25;
		this.textBox_exp_time_g.Text = "100";
		this.label4.AutoSize = true;
		this.label4.Location = new System.Drawing.Point(19, 120);
		this.label4.Name = "label4";
		this.label4.Size = new System.Drawing.Size(65, 12);
		this.label4.TabIndex = 23;
		this.label4.Text = "B 曝光(ms)";
		this.textBox_exp_time_b.Location = new System.Drawing.Point(84, 116);
		this.textBox_exp_time_b.Name = "textBox_exp_time_b";
		this.textBox_exp_time_b.Size = new System.Drawing.Size(156, 21);
		this.textBox_exp_time_b.TabIndex = 25;
		this.textBox_exp_time_b.Text = "100";
		this.textBox_poi.Location = new System.Drawing.Point(84, 164);
		this.textBox_poi.Name = "textBox_poi";
		this.textBox_poi.Size = new System.Drawing.Size(156, 21);
		this.textBox_poi.TabIndex = 28;
		this.label5.AutoSize = true;
		this.label5.Location = new System.Drawing.Point(19, 170);
		this.label5.Name = "label5";
		this.label5.Size = new System.Drawing.Size(47, 12);
		this.label5.TabIndex = 27;
		this.label5.Text = "POI模板";
		this.textBox_poi_filter.Location = new System.Drawing.Point(84, 188);
		this.textBox_poi_filter.Name = "textBox_poi_filter";
		this.textBox_poi_filter.Size = new System.Drawing.Size(156, 21);
		this.textBox_poi_filter.TabIndex = 30;
		this.label6.AutoSize = true;
		this.label6.Location = new System.Drawing.Point(19, 194);
		this.label6.Name = "label6";
		this.label6.Size = new System.Drawing.Size(47, 12);
		this.label6.TabIndex = 29;
		this.label6.Text = "POI过滤";
		this.textBox_poi_revise.Location = new System.Drawing.Point(84, 215);
		this.textBox_poi_revise.Name = "textBox_poi_revise";
		this.textBox_poi_revise.Size = new System.Drawing.Size(156, 21);
		this.textBox_poi_revise.TabIndex = 32;
		this.label7.AutoSize = true;
		this.label7.Location = new System.Drawing.Point(19, 220);
		this.label7.Name = "label7";
		this.label7.Size = new System.Drawing.Size(47, 12);
		this.label7.TabIndex = 31;
		this.label7.Text = "POI修正";
		this.textBox_avg_count.Location = new System.Drawing.Point(84, 11);
		this.textBox_avg_count.Name = "textBox_avg_count";
		this.textBox_avg_count.Size = new System.Drawing.Size(156, 21);
		this.textBox_avg_count.TabIndex = 34;
		this.textBox_avg_count.Text = "1";
		this.label8.AutoSize = true;
		this.label8.Location = new System.Drawing.Point(19, 16);
		this.label8.Name = "label8";
		this.label8.Size = new System.Drawing.Size(53, 12);
		this.label8.TabIndex = 33;
		this.label8.Text = "平均次数";
		this.textBox_gain.Location = new System.Drawing.Point(84, 39);
		this.textBox_gain.Name = "textBox_gain";
		this.textBox_gain.Size = new System.Drawing.Size(156, 21);
		this.textBox_gain.TabIndex = 36;
		this.textBox_gain.Text = "10";
		this.label9.AutoSize = true;
		this.label9.Location = new System.Drawing.Point(19, 44);
		this.label9.Name = "label9";
		this.label9.Size = new System.Drawing.Size(29, 12);
		this.label9.TabIndex = 35;
		this.label9.Text = "增益";
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 12f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(400, 494);
		base.Controls.Add(this.textBox_gain);
		base.Controls.Add(this.label9);
		base.Controls.Add(this.textBox_avg_count);
		base.Controls.Add(this.label8);
		base.Controls.Add(this.textBox_poi_revise);
		base.Controls.Add(this.label7);
		base.Controls.Add(this.textBox_poi_filter);
		base.Controls.Add(this.label6);
		base.Controls.Add(this.textBox_poi);
		base.Controls.Add(this.label5);
		base.Controls.Add(this.button_del);
		base.Controls.Add(this.button_save);
		base.Controls.Add(this.dataGridView1);
		base.Controls.Add(this.button_insert);
		base.Controls.Add(this.textBox_cali_name);
		base.Controls.Add(this.label3);
		base.Controls.Add(this.textBox_exp_time_b);
		base.Controls.Add(this.label4);
		base.Controls.Add(this.textBox_exp_time_g);
		base.Controls.Add(this.label1);
		base.Controls.Add(this.textBox_exp_time_r);
		base.Controls.Add(this.label2);
		base.Controls.Add(this.button_add);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
		base.MaximizeBox = false;
		base.Name = "FormCVCameraProperty";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
		this.Text = "CV相机参数";
		base.Load += new System.EventHandler(FormCVCameraProperty_Load);
		((System.ComponentModel.ISupportInitialize)this.dataGridView1).EndInit();
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}
