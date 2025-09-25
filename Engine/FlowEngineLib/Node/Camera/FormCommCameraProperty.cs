using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using FlowEngineLib.Base;

namespace FlowEngineLib.Node.Camera;

public class FormCommCameraProperty : Form, ILoopFormProperty
{
	private DialogLoopProperty<CommCameraNodeProperty> dialog;

	private IContainer components;

	private TextBox textBox_poi_filter;

	private Label label4;

	private TextBox textBox_poi;

	private Label label1;

	private Button button_insert;

	private TextBox textBox_cali_name;

	private Label label3;

	private TextBox textBox_exp_time;

	private Label label2;

	private Button button_del;

	private Button button_save;

	private DataGridView dataGridView1;

	private Button button_add;

	private CheckBox checkBox_auto_exp;

	private TextBox textBox_poi_revise;

	private Label label5;

	private TextBox textBox_camTemp;

	private Label label8;

	private DataGridViewTextBoxColumn no;

	private DataGridViewTextBoxColumn cam;

	private DataGridViewTextBoxColumn autoExp;

	private DataGridViewTextBoxColumn cmd;

	private DataGridViewTextBoxColumn param;

	private DataGridViewTextBoxColumn POI;

	private DataGridViewTextBoxColumn POIFilter;

	private DataGridViewTextBoxColumn POIRevise;

	public string JsonValue { get; set; }

	public FormCommCameraProperty()
	{
		InitializeComponent();
		dialog = new DialogLoopProperty<CommCameraNodeProperty>(dataGridView1);
	}

	private void FormCommCameraProperty_Load(object sender, EventArgs e)
	{
		dialog.Load(JsonValue);
	}

	private void button_add_Click(object sender, EventArgs e)
	{
		CommCameraNodeProperty pm = BuildProperty();
		dialog.Add(pm);
	}

	private void button_insert_Click(object sender, EventArgs e)
	{
		CommCameraNodeProperty pm = BuildProperty();
		dialog.Insert(pm);
	}

	private CommCameraNodeProperty BuildProperty()
	{
		return new CommCameraNodeProperty
		{
			CamTempName = textBox_camTemp.Text,
			ExpTempName = textBox_exp_time.Text,
			IsAutoExpTime = checkBox_auto_exp.Checked.ToString(),
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
			textBox_camTemp.Text = dataGridView1.SelectedRows[0].Cells[1].Value.ToString();
			checkBox_auto_exp.Checked = bool.Parse(dataGridView1.SelectedRows[0].Cells[2].Value.ToString());
			textBox_exp_time.Text = dataGridView1.SelectedRows[0].Cells[3].Value.ToString();
			textBox_cali_name.Text = dataGridView1.SelectedRows[0].Cells[4].Value.ToString();
			textBox_poi.Text = dataGridView1.SelectedRows[0].Cells[5].Value.ToString();
			textBox_poi_filter.Text = dataGridView1.SelectedRows[0].Cells[6].Value.ToString();
			textBox_poi_revise.Text = dataGridView1.SelectedRows[0].Cells[7].Value.ToString();
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
		this.textBox_poi_filter = new System.Windows.Forms.TextBox();
		this.label4 = new System.Windows.Forms.Label();
		this.textBox_poi = new System.Windows.Forms.TextBox();
		this.label1 = new System.Windows.Forms.Label();
		this.button_insert = new System.Windows.Forms.Button();
		this.textBox_cali_name = new System.Windows.Forms.TextBox();
		this.label3 = new System.Windows.Forms.Label();
		this.textBox_exp_time = new System.Windows.Forms.TextBox();
		this.label2 = new System.Windows.Forms.Label();
		this.button_del = new System.Windows.Forms.Button();
		this.button_save = new System.Windows.Forms.Button();
		this.dataGridView1 = new System.Windows.Forms.DataGridView();
		this.button_add = new System.Windows.Forms.Button();
		this.checkBox_auto_exp = new System.Windows.Forms.CheckBox();
		this.textBox_poi_revise = new System.Windows.Forms.TextBox();
		this.label5 = new System.Windows.Forms.Label();
		this.textBox_camTemp = new System.Windows.Forms.TextBox();
		this.label8 = new System.Windows.Forms.Label();
		this.no = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.cam = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.autoExp = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.cmd = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.param = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.POI = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.POIFilter = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.POIRevise = new System.Windows.Forms.DataGridViewTextBoxColumn();
		((System.ComponentModel.ISupportInitialize)this.dataGridView1).BeginInit();
		base.SuspendLayout();
		this.textBox_poi_filter.Location = new System.Drawing.Point(68, 106);
		this.textBox_poi_filter.Name = "textBox_poi_filter";
		this.textBox_poi_filter.Size = new System.Drawing.Size(156, 21);
		this.textBox_poi_filter.TabIndex = 34;
		this.label4.AutoSize = true;
		this.label4.Location = new System.Drawing.Point(7, 113);
		this.label4.Name = "label4";
		this.label4.Size = new System.Drawing.Size(47, 12);
		this.label4.TabIndex = 33;
		this.label4.Text = "POI过滤";
		this.textBox_poi.Location = new System.Drawing.Point(68, 81);
		this.textBox_poi.Name = "textBox_poi";
		this.textBox_poi.Size = new System.Drawing.Size(156, 21);
		this.textBox_poi.TabIndex = 32;
		this.label1.AutoSize = true;
		this.label1.Location = new System.Drawing.Point(7, 88);
		this.label1.Name = "label1";
		this.label1.Size = new System.Drawing.Size(47, 12);
		this.label1.TabIndex = 31;
		this.label1.Text = "POI模板";
		this.button_insert.Location = new System.Drawing.Point(304, 29);
		this.button_insert.Name = "button_insert";
		this.button_insert.Size = new System.Drawing.Size(75, 23);
		this.button_insert.TabIndex = 30;
		this.button_insert.Text = "插入";
		this.button_insert.UseVisualStyleBackColor = true;
		this.button_insert.Click += new System.EventHandler(button_insert_Click);
		this.textBox_cali_name.Location = new System.Drawing.Point(68, 56);
		this.textBox_cali_name.Name = "textBox_cali_name";
		this.textBox_cali_name.Size = new System.Drawing.Size(156, 21);
		this.textBox_cali_name.TabIndex = 28;
		this.textBox_cali_name.Text = "default1";
		this.label3.AutoSize = true;
		this.label3.Location = new System.Drawing.Point(7, 61);
		this.label3.Name = "label3";
		this.label3.Size = new System.Drawing.Size(53, 12);
		this.label3.TabIndex = 26;
		this.label3.Text = "校正模板";
		this.textBox_exp_time.Location = new System.Drawing.Point(68, 31);
		this.textBox_exp_time.Name = "textBox_exp_time";
		this.textBox_exp_time.Size = new System.Drawing.Size(156, 21);
		this.textBox_exp_time.TabIndex = 29;
		this.label2.AutoSize = true;
		this.label2.Location = new System.Drawing.Point(7, 36);
		this.label2.Name = "label2";
		this.label2.Size = new System.Drawing.Size(53, 12);
		this.label2.TabIndex = 27;
		this.label2.Text = "曝光模板";
		this.button_del.Location = new System.Drawing.Point(304, 55);
		this.button_del.Name = "button_del";
		this.button_del.Size = new System.Drawing.Size(75, 23);
		this.button_del.TabIndex = 25;
		this.button_del.Text = "删除";
		this.button_del.UseVisualStyleBackColor = true;
		this.button_del.Click += new System.EventHandler(button_del_Click);
		this.button_save.Location = new System.Drawing.Point(304, 81);
		this.button_save.Name = "button_save";
		this.button_save.Size = new System.Drawing.Size(75, 23);
		this.button_save.TabIndex = 24;
		this.button_save.Text = "保存";
		this.button_save.UseVisualStyleBackColor = true;
		this.button_save.Click += new System.EventHandler(button_save_Click);
		this.dataGridView1.AllowUserToAddRows = false;
		this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
		this.dataGridView1.Columns.AddRange(this.no, this.cam, this.autoExp, this.cmd, this.param, this.POI, this.POIFilter, this.POIRevise);
		this.dataGridView1.Location = new System.Drawing.Point(0, 160);
		this.dataGridView1.Name = "dataGridView1";
		this.dataGridView1.RowTemplate.Height = 23;
		this.dataGridView1.Size = new System.Drawing.Size(377, 226);
		this.dataGridView1.TabIndex = 23;
		this.dataGridView1.SelectionChanged += new System.EventHandler(dataGridView1_SelectionChanged);
		this.button_add.Location = new System.Drawing.Point(304, 3);
		this.button_add.Name = "button_add";
		this.button_add.Size = new System.Drawing.Size(75, 23);
		this.button_add.TabIndex = 22;
		this.button_add.Text = "添加";
		this.button_add.UseVisualStyleBackColor = true;
		this.button_add.Click += new System.EventHandler(button_add_Click);
		this.checkBox_auto_exp.AutoSize = true;
		this.checkBox_auto_exp.Location = new System.Drawing.Point(230, 35);
		this.checkBox_auto_exp.Name = "checkBox_auto_exp";
		this.checkBox_auto_exp.Size = new System.Drawing.Size(72, 16);
		this.checkBox_auto_exp.TabIndex = 35;
		this.checkBox_auto_exp.Text = "自动曝光";
		this.checkBox_auto_exp.UseVisualStyleBackColor = true;
		this.textBox_poi_revise.Location = new System.Drawing.Point(68, 132);
		this.textBox_poi_revise.Name = "textBox_poi_revise";
		this.textBox_poi_revise.Size = new System.Drawing.Size(156, 21);
		this.textBox_poi_revise.TabIndex = 37;
		this.label5.AutoSize = true;
		this.label5.Location = new System.Drawing.Point(7, 139);
		this.label5.Name = "label5";
		this.label5.Size = new System.Drawing.Size(47, 12);
		this.label5.TabIndex = 36;
		this.label5.Text = "POI修正";
		this.textBox_camTemp.Location = new System.Drawing.Point(68, 6);
		this.textBox_camTemp.Name = "textBox_camTemp";
		this.textBox_camTemp.Size = new System.Drawing.Size(156, 21);
		this.textBox_camTemp.TabIndex = 39;
		this.textBox_camTemp.Text = "Cam.Default";
		this.label8.AutoSize = true;
		this.label8.Location = new System.Drawing.Point(7, 11);
		this.label8.Name = "label8";
		this.label8.Size = new System.Drawing.Size(53, 12);
		this.label8.TabIndex = 38;
		this.label8.Text = "相机模板";
		this.no.HeaderText = "序号";
		this.no.Name = "no";
		this.no.Width = 60;
		this.cam.HeaderText = "相机模板";
		this.cam.Name = "cam";
		this.autoExp.HeaderText = "自动曝光";
		this.autoExp.Name = "autoExp";
		this.cmd.HeaderText = "曝光模板";
		this.cmd.Name = "cmd";
		this.cmd.Width = 80;
		this.param.HeaderText = "校正模板";
		this.param.Name = "param";
		this.param.Width = 150;
		this.POI.HeaderText = "POI";
		this.POI.Name = "POI";
		this.POIFilter.HeaderText = "POI过滤";
		this.POIFilter.Name = "POIFilter";
		this.POIRevise.HeaderText = "POI修正";
		this.POIRevise.Name = "POIRevise";
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 12f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(388, 390);
		base.Controls.Add(this.textBox_camTemp);
		base.Controls.Add(this.label8);
		base.Controls.Add(this.textBox_poi_revise);
		base.Controls.Add(this.label5);
		base.Controls.Add(this.checkBox_auto_exp);
		base.Controls.Add(this.textBox_poi_filter);
		base.Controls.Add(this.label4);
		base.Controls.Add(this.textBox_poi);
		base.Controls.Add(this.label1);
		base.Controls.Add(this.button_insert);
		base.Controls.Add(this.textBox_cali_name);
		base.Controls.Add(this.label3);
		base.Controls.Add(this.textBox_exp_time);
		base.Controls.Add(this.label2);
		base.Controls.Add(this.button_del);
		base.Controls.Add(this.button_save);
		base.Controls.Add(this.dataGridView1);
		base.Controls.Add(this.button_add);
		base.MaximizeBox = false;
		base.Name = "FormCommCameraProperty";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
		this.Text = "通用相机参数";
		base.Load += new System.EventHandler(FormCommCameraProperty_Load);
		((System.ComponentModel.ISupportInitialize)this.dataGridView1).EndInit();
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}
