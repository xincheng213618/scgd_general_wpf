using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using FlowEngineLib.Base;
using FlowEngineLib.Node.PG;

namespace FlowEngineLib.Control;

public class FormPGParam : Form, ILoopFormProperty
{
	private DialogLoopProperty<PGNodeProperty> dialog;

	private IContainer components;

	private DataGridView dataGridView1;

	private DataGridViewTextBoxColumn no;

	private DataGridViewTextBoxColumn cmd;

	private DataGridViewTextBoxColumn param;

	private Button button_del;

	private Button button_save;

	private TextBox textBox_index_frame;

	private Label label2;

	private Label label1;

	private Button button_add;

	private ComboBox comboBox_cmd;

	private Button button_insert;

	public string JsonValue { get; set; }

	public FormPGParam()
	{
		InitializeComponent();
		dialog = new DialogLoopProperty<PGNodeProperty>(dataGridView1);
	}

	private void FormPGParam_Load(object sender, EventArgs e)
	{
		comboBox_cmd.DataSource = Enum.GetNames(typeof(PGCommCmdType));
		comboBox_cmd.SelectedIndex = 0;
		dialog.Load(JsonValue);
	}

	private void button_add_Click(object sender, EventArgs e)
	{
		PGNodeProperty pGNodeProperty = new PGNodeProperty();
		pGNodeProperty.Cmd = (PGCommCmdType)Enum.Parse(typeof(PGCommCmdType), comboBox_cmd.Text);
		if (pGNodeProperty.Cmd == PGCommCmdType.指定)
		{
			pGNodeProperty.Data = new PGParamData();
			pGNodeProperty.Data.IndexFrame = int.Parse(textBox_index_frame.Text);
		}
		dialog.Add(pGNodeProperty);
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

	private void button_insert_Click(object sender, EventArgs e)
	{
		PGNodeProperty pGNodeProperty = new PGNodeProperty();
		pGNodeProperty.Cmd = (PGCommCmdType)Enum.Parse(typeof(PGCommCmdType), comboBox_cmd.Text);
		if (pGNodeProperty.Cmd == PGCommCmdType.指定)
		{
			pGNodeProperty.Data = new PGParamData();
			pGNodeProperty.Data.IndexFrame = int.Parse(textBox_index_frame.Text);
		}
		dialog.Insert(pGNodeProperty);
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
		this.cmd = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.param = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.button_del = new System.Windows.Forms.Button();
		this.button_save = new System.Windows.Forms.Button();
		this.textBox_index_frame = new System.Windows.Forms.TextBox();
		this.label2 = new System.Windows.Forms.Label();
		this.label1 = new System.Windows.Forms.Label();
		this.button_add = new System.Windows.Forms.Button();
		this.comboBox_cmd = new System.Windows.Forms.ComboBox();
		this.button_insert = new System.Windows.Forms.Button();
		((System.ComponentModel.ISupportInitialize)this.dataGridView1).BeginInit();
		base.SuspendLayout();
		this.dataGridView1.AllowUserToAddRows = false;
		this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
		this.dataGridView1.Columns.AddRange(this.no, this.cmd, this.param);
		this.dataGridView1.Location = new System.Drawing.Point(12, 111);
		this.dataGridView1.Name = "dataGridView1";
		this.dataGridView1.RowTemplate.Height = 23;
		this.dataGridView1.Size = new System.Drawing.Size(380, 171);
		this.dataGridView1.TabIndex = 7;
		this.no.HeaderText = "序号";
		this.no.Name = "no";
		this.no.Width = 60;
		this.cmd.HeaderText = "命令";
		this.cmd.Name = "cmd";
		this.cmd.Width = 80;
		this.param.HeaderText = "参数";
		this.param.Name = "param";
		this.param.Width = 560;
		this.button_del.Location = new System.Drawing.Point(294, 57);
		this.button_del.Name = "button_del";
		this.button_del.Size = new System.Drawing.Size(75, 23);
		this.button_del.TabIndex = 15;
		this.button_del.Text = "删除";
		this.button_del.UseVisualStyleBackColor = true;
		this.button_del.Click += new System.EventHandler(button_del_Click);
		this.button_save.Location = new System.Drawing.Point(294, 81);
		this.button_save.Name = "button_save";
		this.button_save.Size = new System.Drawing.Size(75, 23);
		this.button_save.TabIndex = 14;
		this.button_save.Text = "保存";
		this.button_save.UseVisualStyleBackColor = true;
		this.button_save.Click += new System.EventHandler(button_save_Click);
		this.textBox_index_frame.Location = new System.Drawing.Point(82, 54);
		this.textBox_index_frame.Name = "textBox_index_frame";
		this.textBox_index_frame.Size = new System.Drawing.Size(156, 21);
		this.textBox_index_frame.TabIndex = 13;
		this.textBox_index_frame.Text = "1";
		this.label2.AutoSize = true;
		this.label2.Location = new System.Drawing.Point(17, 59);
		this.label2.Name = "label2";
		this.label2.Size = new System.Drawing.Size(53, 12);
		this.label2.TabIndex = 12;
		this.label2.Text = "指定画面";
		this.label1.AutoSize = true;
		this.label1.Location = new System.Drawing.Point(41, 28);
		this.label1.Name = "label1";
		this.label1.Size = new System.Drawing.Size(29, 12);
		this.label1.TabIndex = 11;
		this.label1.Text = "命令";
		this.button_add.Location = new System.Drawing.Point(294, 9);
		this.button_add.Name = "button_add";
		this.button_add.Size = new System.Drawing.Size(75, 23);
		this.button_add.TabIndex = 10;
		this.button_add.Text = "添加";
		this.button_add.UseVisualStyleBackColor = true;
		this.button_add.Click += new System.EventHandler(button_add_Click);
		this.comboBox_cmd.FormattingEnabled = true;
		this.comboBox_cmd.Location = new System.Drawing.Point(82, 25);
		this.comboBox_cmd.Name = "comboBox_cmd";
		this.comboBox_cmd.Size = new System.Drawing.Size(156, 20);
		this.comboBox_cmd.TabIndex = 9;
		this.button_insert.Location = new System.Drawing.Point(294, 33);
		this.button_insert.Name = "button_insert";
		this.button_insert.Size = new System.Drawing.Size(75, 23);
		this.button_insert.TabIndex = 16;
		this.button_insert.Text = "插入";
		this.button_insert.UseVisualStyleBackColor = true;
		this.button_insert.Click += new System.EventHandler(button_insert_Click);
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 12f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(411, 294);
		base.Controls.Add(this.button_insert);
		base.Controls.Add(this.button_del);
		base.Controls.Add(this.button_save);
		base.Controls.Add(this.textBox_index_frame);
		base.Controls.Add(this.label2);
		base.Controls.Add(this.label1);
		base.Controls.Add(this.button_add);
		base.Controls.Add(this.comboBox_cmd);
		base.Controls.Add(this.dataGridView1);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.Name = "FormPGParam";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
		this.Text = "PG参数";
		base.Load += new System.EventHandler(FormPGParam_Load);
		((System.ComponentModel.ISupportInitialize)this.dataGridView1).EndInit();
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}
