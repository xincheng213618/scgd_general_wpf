using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using FlowEngineLib.Base;
using FlowEngineLib.Node.Spectrum;
using Newtonsoft.Json;

namespace FlowEngineLib.Control;

public class FormSpectumParam : Form, ILoopFormProperty
{
	private DialogLoopProperty<SpectrumNodeProperty> dialog;

	private IContainer components;

	private ComboBox comboBox_cmd;

	private Button button_add;

	private Label label1;

	private Label label2;

	private TextBox textBox_integral_time;

	private Label label3;

	private TextBox textBox_number_of_average;

	private CheckBox checkBox_auto_integration;

	private CheckBox checkBox_self_adaption;

	private DataGridView dataGridView1;

	private DataGridViewTextBoxColumn no;

	private DataGridViewTextBoxColumn cmd;

	private DataGridViewTextBoxColumn param;

	private Button button_save;

	private Button button_del;

	private Button button_insert;

	private CheckBox checkBox_AutoInitDark;

	public string JsonValue { get; set; }

	public FormSpectumParam()
	{
		InitializeComponent();
		dialog = new DialogLoopProperty<SpectrumNodeProperty>(dataGridView1);
	}

	private void FormSpectumParam_Load(object sender, EventArgs e)
	{
		dialog.Load(JsonValue);
		comboBox_cmd.DataSource = Enum.GetNames(typeof(SPCommCmdType));
		comboBox_cmd.SelectedIndex = 0;
	}

	private void button_add_Click(object sender, EventArgs e)
	{
		SpectrumNodeProperty spectrumNodeProperty = new SpectrumNodeProperty();
		spectrumNodeProperty.Cmd = (SPCommCmdType)Enum.Parse(typeof(SPCommCmdType), comboBox_cmd.Text);
		if (comboBox_cmd.SelectedIndex == 0)
		{
			spectrumNodeProperty.Data = new SpectrumParamData();
			spectrumNodeProperty.Data.IntegralTime = int.Parse(textBox_integral_time.Text);
			spectrumNodeProperty.Data.NumberOfAverage = int.Parse(textBox_number_of_average.Text);
			spectrumNodeProperty.Data.AutoIntegration = checkBox_auto_integration.Checked;
			spectrumNodeProperty.Data.SelfAdaptionInitDark = checkBox_self_adaption.Checked;
			spectrumNodeProperty.Data.AutoInitDark = checkBox_AutoInitDark.Checked;
		}
		dialog.Add(spectrumNodeProperty);
	}

	private void button_save_Click(object sender, EventArgs e)
	{
		JsonValue = dialog.Save();
		base.DialogResult = DialogResult.OK;
	}

	private void button_del_Click(object sender, EventArgs e)
	{
		dialog.Remove();
	}

	private void dataGridView1_SelectionChanged(object sender, EventArgs e)
	{
		if (dataGridView1.SelectedRows.Count != 1)
		{
			return;
		}
		comboBox_cmd.Text = dataGridView1.SelectedRows[0].Cells[1].Value.ToString();
		string text = dataGridView1.SelectedRows[0].Cells[2].Value?.ToString();
		if (!string.IsNullOrEmpty(text))
		{
			SpectrumParamData spectrumParamData = JsonConvert.DeserializeObject<SpectrumParamData>(text);
			if (spectrumParamData != null)
			{
				textBox_integral_time.Text = spectrumParamData.IntegralTime.ToString();
				textBox_number_of_average.Text = spectrumParamData.NumberOfAverage.ToString();
				checkBox_auto_integration.Checked = spectrumParamData.AutoIntegration;
				checkBox_self_adaption.Checked = spectrumParamData.SelfAdaptionInitDark;
				checkBox_AutoInitDark.Checked = spectrumParamData.AutoInitDark;
			}
		}
	}

	private void button_insert_Click(object sender, EventArgs e)
	{
		SpectrumNodeProperty spectrumNodeProperty = new SpectrumNodeProperty();
		spectrumNodeProperty.Cmd = (SPCommCmdType)Enum.Parse(typeof(SPCommCmdType), comboBox_cmd.Text);
		if (comboBox_cmd.SelectedIndex == 0)
		{
			spectrumNodeProperty.Data = new SpectrumParamData();
			spectrumNodeProperty.Data.IntegralTime = int.Parse(textBox_integral_time.Text);
			spectrumNodeProperty.Data.NumberOfAverage = int.Parse(textBox_number_of_average.Text);
			spectrumNodeProperty.Data.AutoIntegration = checkBox_auto_integration.Checked;
			spectrumNodeProperty.Data.SelfAdaptionInitDark = checkBox_self_adaption.Checked;
			spectrumNodeProperty.Data.AutoInitDark = checkBox_AutoInitDark.Checked;
		}
		dialog.Insert(spectrumNodeProperty);
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
		this.comboBox_cmd = new System.Windows.Forms.ComboBox();
		this.button_add = new System.Windows.Forms.Button();
		this.label1 = new System.Windows.Forms.Label();
		this.label2 = new System.Windows.Forms.Label();
		this.textBox_integral_time = new System.Windows.Forms.TextBox();
		this.label3 = new System.Windows.Forms.Label();
		this.textBox_number_of_average = new System.Windows.Forms.TextBox();
		this.checkBox_auto_integration = new System.Windows.Forms.CheckBox();
		this.checkBox_self_adaption = new System.Windows.Forms.CheckBox();
		this.dataGridView1 = new System.Windows.Forms.DataGridView();
		this.no = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.cmd = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.param = new System.Windows.Forms.DataGridViewTextBoxColumn();
		this.button_save = new System.Windows.Forms.Button();
		this.button_del = new System.Windows.Forms.Button();
		this.button_insert = new System.Windows.Forms.Button();
		this.checkBox_AutoInitDark = new System.Windows.Forms.CheckBox();
		((System.ComponentModel.ISupportInitialize)this.dataGridView1).BeginInit();
		base.SuspendLayout();
		this.comboBox_cmd.FormattingEnabled = true;
		this.comboBox_cmd.Items.AddRange(new object[2] { "检测", "校零" });
		this.comboBox_cmd.Location = new System.Drawing.Point(76, 12);
		this.comboBox_cmd.Name = "comboBox_cmd";
		this.comboBox_cmd.Size = new System.Drawing.Size(156, 20);
		this.comboBox_cmd.TabIndex = 0;
		this.button_add.Location = new System.Drawing.Point(288, 12);
		this.button_add.Name = "button_add";
		this.button_add.Size = new System.Drawing.Size(75, 23);
		this.button_add.TabIndex = 1;
		this.button_add.Text = "添加";
		this.button_add.UseVisualStyleBackColor = true;
		this.button_add.Click += new System.EventHandler(button_add_Click);
		this.label1.AutoSize = true;
		this.label1.Location = new System.Drawing.Point(35, 15);
		this.label1.Name = "label1";
		this.label1.Size = new System.Drawing.Size(29, 12);
		this.label1.TabIndex = 2;
		this.label1.Text = "命令";
		this.label2.AutoSize = true;
		this.label2.Location = new System.Drawing.Point(11, 46);
		this.label2.Name = "label2";
		this.label2.Size = new System.Drawing.Size(53, 12);
		this.label2.TabIndex = 3;
		this.label2.Text = "积分时间";
		this.textBox_integral_time.Location = new System.Drawing.Point(76, 41);
		this.textBox_integral_time.Name = "textBox_integral_time";
		this.textBox_integral_time.Size = new System.Drawing.Size(156, 21);
		this.textBox_integral_time.TabIndex = 4;
		this.textBox_integral_time.Text = "100";
		this.label3.AutoSize = true;
		this.label3.Location = new System.Drawing.Point(11, 73);
		this.label3.Name = "label3";
		this.label3.Size = new System.Drawing.Size(53, 12);
		this.label3.TabIndex = 3;
		this.label3.Text = "平均次数";
		this.textBox_number_of_average.Location = new System.Drawing.Point(76, 68);
		this.textBox_number_of_average.Name = "textBox_number_of_average";
		this.textBox_number_of_average.Size = new System.Drawing.Size(156, 21);
		this.textBox_number_of_average.TabIndex = 4;
		this.textBox_number_of_average.Text = "1";
		this.checkBox_auto_integration.AutoSize = true;
		this.checkBox_auto_integration.Location = new System.Drawing.Point(19, 109);
		this.checkBox_auto_integration.Name = "checkBox_auto_integration";
		this.checkBox_auto_integration.Size = new System.Drawing.Size(72, 16);
		this.checkBox_auto_integration.TabIndex = 5;
		this.checkBox_auto_integration.Text = "自动积分";
		this.checkBox_auto_integration.UseVisualStyleBackColor = true;
		this.checkBox_self_adaption.AutoSize = true;
		this.checkBox_self_adaption.Location = new System.Drawing.Point(106, 109);
		this.checkBox_self_adaption.Name = "checkBox_self_adaption";
		this.checkBox_self_adaption.Size = new System.Drawing.Size(84, 16);
		this.checkBox_self_adaption.TabIndex = 5;
		this.checkBox_self_adaption.Text = "自适应校零";
		this.checkBox_self_adaption.UseVisualStyleBackColor = true;
		this.dataGridView1.AllowUserToAddRows = false;
		this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
		this.dataGridView1.Columns.AddRange(this.no, this.cmd, this.param);
		this.dataGridView1.Location = new System.Drawing.Point(13, 142);
		this.dataGridView1.Name = "dataGridView1";
		this.dataGridView1.RowTemplate.Height = 23;
		this.dataGridView1.Size = new System.Drawing.Size(377, 162);
		this.dataGridView1.TabIndex = 6;
		this.dataGridView1.SelectionChanged += new System.EventHandler(dataGridView1_SelectionChanged);
		this.no.HeaderText = "序号";
		this.no.Name = "no";
		this.no.Width = 60;
		this.cmd.HeaderText = "命令";
		this.cmd.Name = "cmd";
		this.cmd.Width = 80;
		this.param.HeaderText = "参数";
		this.param.Name = "param";
		this.param.Width = 560;
		this.button_save.Location = new System.Drawing.Point(288, 90);
		this.button_save.Name = "button_save";
		this.button_save.Size = new System.Drawing.Size(75, 23);
		this.button_save.TabIndex = 7;
		this.button_save.Text = "保存";
		this.button_save.UseVisualStyleBackColor = true;
		this.button_save.Click += new System.EventHandler(button_save_Click);
		this.button_del.Location = new System.Drawing.Point(288, 64);
		this.button_del.Name = "button_del";
		this.button_del.Size = new System.Drawing.Size(75, 23);
		this.button_del.TabIndex = 8;
		this.button_del.Text = "删除";
		this.button_del.UseVisualStyleBackColor = true;
		this.button_del.Click += new System.EventHandler(button_del_Click);
		this.button_insert.Location = new System.Drawing.Point(288, 38);
		this.button_insert.Name = "button_insert";
		this.button_insert.Size = new System.Drawing.Size(75, 23);
		this.button_insert.TabIndex = 1;
		this.button_insert.Text = "插入";
		this.button_insert.UseVisualStyleBackColor = true;
		this.button_insert.Click += new System.EventHandler(button_insert_Click);
		this.checkBox_AutoInitDark.AutoSize = true;
		this.checkBox_AutoInitDark.Location = new System.Drawing.Point(205, 109);
		this.checkBox_AutoInitDark.Name = "checkBox_AutoInitDark";
		this.checkBox_AutoInitDark.Size = new System.Drawing.Size(72, 16);
		this.checkBox_AutoInitDark.TabIndex = 5;
		this.checkBox_AutoInitDark.Text = "自动校零";
		this.checkBox_AutoInitDark.UseVisualStyleBackColor = true;
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 12f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(402, 316);
		base.Controls.Add(this.button_del);
		base.Controls.Add(this.button_save);
		base.Controls.Add(this.dataGridView1);
		base.Controls.Add(this.checkBox_AutoInitDark);
		base.Controls.Add(this.checkBox_self_adaption);
		base.Controls.Add(this.checkBox_auto_integration);
		base.Controls.Add(this.textBox_number_of_average);
		base.Controls.Add(this.label3);
		base.Controls.Add(this.textBox_integral_time);
		base.Controls.Add(this.label2);
		base.Controls.Add(this.label1);
		base.Controls.Add(this.button_insert);
		base.Controls.Add(this.button_add);
		base.Controls.Add(this.comboBox_cmd);
		base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.Name = "FormSpectumParam";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
		this.Text = "光谱仪参数";
		base.Load += new System.EventHandler(FormSpectumParam_Load);
		((System.ComponentModel.ISupportInitialize)this.dataGridView1).EndInit();
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}
