using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using FlowEngineLib.Camera;
using Newtonsoft.Json;

namespace FlowEngineLib.Control;

public class FormChannel : Form
{
	private string[] szTypeText = new string[3] { "R(红)", "G(绿)", "B(蓝)" };

	private string[] szTypeCode = new string[3] { "X", "Y", "Z" };

	private TextBox[] boxes;

	private TextBox[] exp_boxes;

	public string ChannnelJson;

	private IContainer components;

	private ComboBox comboBox2;

	private TextBox tb_Channel2cfwPort;

	private Label label7;

	private TextBox tb_Channel2Title;

	private Label label8;

	private GroupBox gb_Channel2;

	private Label label9;

	private ComboBox comboBox3;

	private GroupBox gb_Channel3;

	private TextBox tb_Channel3cfwPort;

	private Label label4;

	private TextBox tb_Channel3Title;

	private Label label6;

	private Label label5;

	private Button btn_Confirm;

	private Label label1;

	private ComboBox comboBox1;

	private TextBox tb_Channel1cfwPort;

	private Label label3;

	private TextBox tb_Channel1Title;

	private Label label2;

	private Button btn_Cancel;

	private GroupBox gb_Channel1;

	private TextBox tb_exp_2;

	private Label label11;

	private TextBox tb_exp_3;

	private Label label12;

	private TextBox tb_exp_1;

	private Label label10;

	public FormChannel()
	{
		InitializeComponent();
		string[] array = szTypeCode;
		foreach (string item in array)
		{
			comboBox1.Items.Add(item);
			comboBox2.Items.Add(item);
			comboBox3.Items.Add(item);
		}
		boxes = new TextBox[3] { tb_Channel1cfwPort, tb_Channel2cfwPort, tb_Channel3cfwPort };
		exp_boxes = new TextBox[3] { tb_exp_1, tb_exp_2, tb_exp_3 };
	}

	private void FormChannel_Load(object sender, EventArgs e)
	{
		tb_exp_1.Text = "100";
		tb_exp_2.Text = "100";
		tb_exp_3.Text = "100";
		comboBox1.SelectedIndex = 0;
		comboBox2.SelectedIndex = 1;
		comboBox3.SelectedIndex = 2;
		if (ChannnelJson != null && ChannnelJson.Length > 0)
		{
			List<ChannelData> list = JsonConvert.DeserializeObject<List<ChannelData>>(ChannnelJson);
			for (int i = 0; i < list.Count; i++)
			{
				ChannelData channelData = list[i];
				boxes[i].Text = channelData.FWPort.ToString();
				exp_boxes[i].Text = channelData.Temp.ToString();
			}
		}
	}

	private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
	{
		if (comboBox1.SelectedIndex < 3 && comboBox1.SelectedIndex >= 0)
		{
			tb_Channel1Title.Text = szTypeText[comboBox1.SelectedIndex];
		}
	}

	private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
	{
		if (comboBox2.SelectedIndex < 4 && comboBox2.SelectedIndex >= 0)
		{
			tb_Channel2Title.Text = szTypeText[comboBox2.SelectedIndex];
		}
	}

	private void comboBox3_SelectedIndexChanged(object sender, EventArgs e)
	{
		if (comboBox3.SelectedIndex < 4 && comboBox3.SelectedIndex >= 0)
		{
			tb_Channel3Title.Text = szTypeText[comboBox3.SelectedIndex];
		}
	}

	private void btn_Confirm_Click(object sender, EventArgs e)
	{
		List<ChannelData> list = new List<ChannelData>();
		for (int i = 0; i < szTypeCode.Length; i++)
		{
			ChannelData item = new ChannelData(szTypeCode[i], int.Parse(boxes[i].Text), int.Parse(exp_boxes[i].Text));
			list.Add(item);
		}
		ChannnelJson = JsonConvert.SerializeObject((object)list);
		base.DialogResult = DialogResult.OK;
	}

	private void btn_Cancel_Click(object sender, EventArgs e)
	{
		base.DialogResult = DialogResult.Cancel;
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
		this.comboBox2 = new System.Windows.Forms.ComboBox();
		this.tb_Channel2cfwPort = new System.Windows.Forms.TextBox();
		this.label7 = new System.Windows.Forms.Label();
		this.tb_Channel2Title = new System.Windows.Forms.TextBox();
		this.label8 = new System.Windows.Forms.Label();
		this.gb_Channel2 = new System.Windows.Forms.GroupBox();
		this.tb_exp_2 = new System.Windows.Forms.TextBox();
		this.label11 = new System.Windows.Forms.Label();
		this.label9 = new System.Windows.Forms.Label();
		this.comboBox3 = new System.Windows.Forms.ComboBox();
		this.gb_Channel3 = new System.Windows.Forms.GroupBox();
		this.tb_exp_3 = new System.Windows.Forms.TextBox();
		this.tb_Channel3cfwPort = new System.Windows.Forms.TextBox();
		this.label4 = new System.Windows.Forms.Label();
		this.label12 = new System.Windows.Forms.Label();
		this.tb_Channel3Title = new System.Windows.Forms.TextBox();
		this.label6 = new System.Windows.Forms.Label();
		this.label5 = new System.Windows.Forms.Label();
		this.btn_Confirm = new System.Windows.Forms.Button();
		this.label1 = new System.Windows.Forms.Label();
		this.comboBox1 = new System.Windows.Forms.ComboBox();
		this.tb_Channel1cfwPort = new System.Windows.Forms.TextBox();
		this.label3 = new System.Windows.Forms.Label();
		this.tb_Channel1Title = new System.Windows.Forms.TextBox();
		this.label2 = new System.Windows.Forms.Label();
		this.btn_Cancel = new System.Windows.Forms.Button();
		this.gb_Channel1 = new System.Windows.Forms.GroupBox();
		this.tb_exp_1 = new System.Windows.Forms.TextBox();
		this.label10 = new System.Windows.Forms.Label();
		this.gb_Channel2.SuspendLayout();
		this.gb_Channel3.SuspendLayout();
		this.gb_Channel1.SuspendLayout();
		base.SuspendLayout();
		this.comboBox2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
		this.comboBox2.FormattingEnabled = true;
		this.comboBox2.Location = new System.Drawing.Point(225, 123);
		this.comboBox2.Name = "comboBox2";
		this.comboBox2.Size = new System.Drawing.Size(85, 20);
		this.comboBox2.TabIndex = 8;
		this.comboBox2.Visible = false;
		this.comboBox2.SelectedIndexChanged += new System.EventHandler(comboBox2_SelectedIndexChanged);
		this.tb_Channel2cfwPort.Location = new System.Drawing.Point(53, 17);
		this.tb_Channel2cfwPort.Name = "tb_Channel2cfwPort";
		this.tb_Channel2cfwPort.Size = new System.Drawing.Size(85, 21);
		this.tb_Channel2cfwPort.TabIndex = 6;
		this.tb_Channel2cfwPort.Text = "1";
		this.label7.AutoSize = true;
		this.label7.Location = new System.Drawing.Point(176, 128);
		this.label7.Name = "label7";
		this.label7.Size = new System.Drawing.Size(41, 12);
		this.label7.TabIndex = 4;
		this.label7.Text = "chType";
		this.label7.Visible = false;
		this.tb_Channel2Title.Location = new System.Drawing.Point(53, 46);
		this.tb_Channel2Title.Name = "tb_Channel2Title";
		this.tb_Channel2Title.ReadOnly = true;
		this.tb_Channel2Title.Size = new System.Drawing.Size(85, 21);
		this.tb_Channel2Title.TabIndex = 15;
		this.label8.AutoSize = true;
		this.label8.Location = new System.Drawing.Point(4, 23);
		this.label8.Name = "label8";
		this.label8.Size = new System.Drawing.Size(47, 12);
		this.label8.TabIndex = 2;
		this.label8.Text = "cfwport";
		this.gb_Channel2.Controls.Add(this.tb_exp_2);
		this.gb_Channel2.Controls.Add(this.tb_Channel2cfwPort);
		this.gb_Channel2.Controls.Add(this.label11);
		this.gb_Channel2.Controls.Add(this.tb_Channel2Title);
		this.gb_Channel2.Controls.Add(this.label9);
		this.gb_Channel2.Controls.Add(this.label8);
		this.gb_Channel2.Location = new System.Drawing.Point(172, 21);
		this.gb_Channel2.Name = "gb_Channel2";
		this.gb_Channel2.Size = new System.Drawing.Size(149, 115);
		this.gb_Channel2.TabIndex = 10;
		this.gb_Channel2.TabStop = false;
		this.gb_Channel2.Text = "通道 Y";
		this.tb_exp_2.Location = new System.Drawing.Point(53, 77);
		this.tb_exp_2.Name = "tb_exp_2";
		this.tb_exp_2.Size = new System.Drawing.Size(85, 21);
		this.tb_exp_2.TabIndex = 6;
		this.label11.AutoSize = true;
		this.label11.Location = new System.Drawing.Point(3, 83);
		this.label11.Name = "label11";
		this.label11.Size = new System.Drawing.Size(53, 12);
		this.label11.TabIndex = 2;
		this.label11.Text = "exposure";
		this.label9.AutoSize = true;
		this.label9.Location = new System.Drawing.Point(4, 53);
		this.label9.Name = "label9";
		this.label9.Size = new System.Drawing.Size(35, 12);
		this.label9.TabIndex = 0;
		this.label9.Text = "Title";
		this.comboBox3.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
		this.comboBox3.FormattingEnabled = true;
		this.comboBox3.Location = new System.Drawing.Point(387, 123);
		this.comboBox3.Name = "comboBox3";
		this.comboBox3.Size = new System.Drawing.Size(85, 20);
		this.comboBox3.TabIndex = 9;
		this.comboBox3.Visible = false;
		this.comboBox3.SelectedIndexChanged += new System.EventHandler(comboBox3_SelectedIndexChanged);
		this.gb_Channel3.Controls.Add(this.tb_exp_3);
		this.gb_Channel3.Controls.Add(this.tb_Channel3cfwPort);
		this.gb_Channel3.Controls.Add(this.label12);
		this.gb_Channel3.Controls.Add(this.tb_Channel3Title);
		this.gb_Channel3.Controls.Add(this.label6);
		this.gb_Channel3.Controls.Add(this.label5);
		this.gb_Channel3.Location = new System.Drawing.Point(335, 21);
		this.gb_Channel3.Name = "gb_Channel3";
		this.gb_Channel3.Size = new System.Drawing.Size(149, 115);
		this.gb_Channel3.TabIndex = 12;
		this.gb_Channel3.TabStop = false;
		this.gb_Channel3.Text = "通道 Z";
		this.tb_exp_3.Location = new System.Drawing.Point(52, 77);
		this.tb_exp_3.Name = "tb_exp_3";
		this.tb_exp_3.Size = new System.Drawing.Size(85, 21);
		this.tb_exp_3.TabIndex = 6;
		this.tb_Channel3cfwPort.Location = new System.Drawing.Point(52, 17);
		this.tb_Channel3cfwPort.Name = "tb_Channel3cfwPort";
		this.tb_Channel3cfwPort.Size = new System.Drawing.Size(85, 21);
		this.tb_Channel3cfwPort.TabIndex = 6;
		this.tb_Channel3cfwPort.Text = "2";
		this.label4.AutoSize = true;
		this.label4.Location = new System.Drawing.Point(341, 128);
		this.label4.Name = "label4";
		this.label4.Size = new System.Drawing.Size(41, 12);
		this.label4.TabIndex = 4;
		this.label4.Text = "chType";
		this.label4.Visible = false;
		this.label12.AutoSize = true;
		this.label12.Location = new System.Drawing.Point(2, 83);
		this.label12.Name = "label12";
		this.label12.Size = new System.Drawing.Size(53, 12);
		this.label12.TabIndex = 2;
		this.label12.Text = "exposure";
		this.tb_Channel3Title.Location = new System.Drawing.Point(52, 46);
		this.tb_Channel3Title.Name = "tb_Channel3Title";
		this.tb_Channel3Title.ReadOnly = true;
		this.tb_Channel3Title.Size = new System.Drawing.Size(85, 21);
		this.tb_Channel3Title.TabIndex = 1;
		this.label6.AutoSize = true;
		this.label6.Location = new System.Drawing.Point(6, 53);
		this.label6.Name = "label6";
		this.label6.Size = new System.Drawing.Size(35, 12);
		this.label6.TabIndex = 0;
		this.label6.Text = "Title";
		this.label5.AutoSize = true;
		this.label5.Location = new System.Drawing.Point(6, 23);
		this.label5.Name = "label5";
		this.label5.Size = new System.Drawing.Size(47, 12);
		this.label5.TabIndex = 2;
		this.label5.Text = "cfwport";
		this.btn_Confirm.Location = new System.Drawing.Point(166, 147);
		this.btn_Confirm.Name = "btn_Confirm";
		this.btn_Confirm.Size = new System.Drawing.Size(75, 23);
		this.btn_Confirm.TabIndex = 13;
		this.btn_Confirm.Text = "确定";
		this.btn_Confirm.UseVisualStyleBackColor = true;
		this.btn_Confirm.Click += new System.EventHandler(btn_Confirm_Click);
		this.label1.AutoSize = true;
		this.label1.Location = new System.Drawing.Point(3, 53);
		this.label1.Name = "label1";
		this.label1.Size = new System.Drawing.Size(35, 12);
		this.label1.TabIndex = 0;
		this.label1.Text = "Title";
		this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
		this.comboBox1.FormattingEnabled = true;
		this.comboBox1.Location = new System.Drawing.Point(65, 123);
		this.comboBox1.Name = "comboBox1";
		this.comboBox1.Size = new System.Drawing.Size(85, 20);
		this.comboBox1.TabIndex = 7;
		this.comboBox1.Visible = false;
		this.comboBox1.SelectedIndexChanged += new System.EventHandler(comboBox1_SelectedIndexChanged);
		this.tb_Channel1cfwPort.Location = new System.Drawing.Point(53, 17);
		this.tb_Channel1cfwPort.Name = "tb_Channel1cfwPort";
		this.tb_Channel1cfwPort.Size = new System.Drawing.Size(85, 21);
		this.tb_Channel1cfwPort.TabIndex = 6;
		this.tb_Channel1cfwPort.Text = "0";
		this.label3.AutoSize = true;
		this.label3.Location = new System.Drawing.Point(15, 128);
		this.label3.Name = "label3";
		this.label3.Size = new System.Drawing.Size(41, 12);
		this.label3.TabIndex = 4;
		this.label3.Text = "chType";
		this.label3.Visible = false;
		this.tb_Channel1Title.Location = new System.Drawing.Point(53, 46);
		this.tb_Channel1Title.Name = "tb_Channel1Title";
		this.tb_Channel1Title.ReadOnly = true;
		this.tb_Channel1Title.Size = new System.Drawing.Size(85, 21);
		this.tb_Channel1Title.TabIndex = 10;
		this.label2.AutoSize = true;
		this.label2.Location = new System.Drawing.Point(3, 23);
		this.label2.Name = "label2";
		this.label2.Size = new System.Drawing.Size(47, 12);
		this.label2.TabIndex = 2;
		this.label2.Text = "cfwport";
		this.btn_Cancel.Location = new System.Drawing.Point(278, 147);
		this.btn_Cancel.Name = "btn_Cancel";
		this.btn_Cancel.Size = new System.Drawing.Size(75, 23);
		this.btn_Cancel.TabIndex = 14;
		this.btn_Cancel.Text = "取消";
		this.btn_Cancel.UseVisualStyleBackColor = true;
		this.btn_Cancel.Click += new System.EventHandler(btn_Cancel_Click);
		this.gb_Channel1.Controls.Add(this.tb_exp_1);
		this.gb_Channel1.Controls.Add(this.tb_Channel1cfwPort);
		this.gb_Channel1.Controls.Add(this.label10);
		this.gb_Channel1.Controls.Add(this.tb_Channel1Title);
		this.gb_Channel1.Controls.Add(this.label2);
		this.gb_Channel1.Controls.Add(this.label1);
		this.gb_Channel1.Location = new System.Drawing.Point(12, 21);
		this.gb_Channel1.Name = "gb_Channel1";
		this.gb_Channel1.Size = new System.Drawing.Size(149, 115);
		this.gb_Channel1.TabIndex = 11;
		this.gb_Channel1.TabStop = false;
		this.gb_Channel1.Text = "通道 X";
		this.tb_exp_1.Location = new System.Drawing.Point(53, 77);
		this.tb_exp_1.Name = "tb_exp_1";
		this.tb_exp_1.Size = new System.Drawing.Size(85, 21);
		this.tb_exp_1.TabIndex = 6;
		this.label10.AutoSize = true;
		this.label10.Location = new System.Drawing.Point(3, 83);
		this.label10.Name = "label10";
		this.label10.Size = new System.Drawing.Size(53, 12);
		this.label10.TabIndex = 2;
		this.label10.Text = "exposure";
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 12f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(505, 173);
		base.Controls.Add(this.comboBox3);
		base.Controls.Add(this.comboBox1);
		base.Controls.Add(this.comboBox2);
		base.Controls.Add(this.label4);
		base.Controls.Add(this.gb_Channel2);
		base.Controls.Add(this.label3);
		base.Controls.Add(this.gb_Channel3);
		base.Controls.Add(this.label7);
		base.Controls.Add(this.btn_Confirm);
		base.Controls.Add(this.btn_Cancel);
		base.Controls.Add(this.gb_Channel1);
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.Name = "FormChannel";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
		this.Text = "FormChannel";
		base.Load += new System.EventHandler(FormChannel_Load);
		this.gb_Channel2.ResumeLayout(false);
		this.gb_Channel2.PerformLayout();
		this.gb_Channel3.ResumeLayout(false);
		this.gb_Channel3.PerformLayout();
		this.gb_Channel1.ResumeLayout(false);
		this.gb_Channel1.PerformLayout();
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}
