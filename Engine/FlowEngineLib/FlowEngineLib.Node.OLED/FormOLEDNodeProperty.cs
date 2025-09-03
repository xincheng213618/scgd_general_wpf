using System;
using System.ComponentModel;
using System.Windows.Forms;
using FlowEngineLib.Node.Algorithm;
using Newtonsoft.Json;

namespace FlowEngineLib.Node.OLED;

public class FormOLEDNodeProperty : Form
{
	private IContainer components;

	private Label label1;

	private Label label2;

	private Label label3;

	private Label label4;

	private TextBox textBox_lefttop_x;

	private TextBox textBox_lefttop_y;

	private TextBox textBox_righttop_x;

	private TextBox textBox_righttop_y;

	private TextBox textBox_leftbottom_x;

	private TextBox textBox_leftbottom_y;

	private TextBox textBox_rightbottom_x;

	private TextBox textBox_rightbottom_y;

	private Button button_ok;

	private Button button_close;

	public string JsonValue { get; set; }

	public FormOLEDNodeProperty()
	{
		InitializeComponent();
	}

	private void FormOLEDNodeProperty_Load(object sender, EventArgs e)
	{
		if (!string.IsNullOrEmpty(JsonValue))
		{
			PointFloat[] array = JsonConvert.DeserializeObject<PointFloat[]>(JsonValue);
			if (array != null && array.Length == 4)
			{
				textBox_lefttop_x.Text = array[0].X.ToString();
				textBox_lefttop_y.Text = array[0].Y.ToString();
				textBox_righttop_x.Text = array[1].X.ToString();
				textBox_righttop_y.Text = array[1].Y.ToString();
				textBox_rightbottom_x.Text = array[2].X.ToString();
				textBox_rightbottom_y.Text = array[2].Y.ToString();
				textBox_leftbottom_x.Text = array[3].X.ToString();
				textBox_leftbottom_y.Text = array[3].Y.ToString();
			}
		}
	}

	private void button_ok_Click(object sender, EventArgs e)
	{
		JsonValue = JsonConvert.SerializeObject(new PointFloat[4]
		{
			new PointFloat
			{
				X = int.Parse(textBox_lefttop_x.Text),
				Y = int.Parse(textBox_lefttop_y.Text)
			},
			new PointFloat
			{
				X = int.Parse(textBox_righttop_x.Text),
				Y = int.Parse(textBox_righttop_y.Text)
			},
			new PointFloat
			{
				X = int.Parse(textBox_rightbottom_x.Text),
				Y = int.Parse(textBox_rightbottom_y.Text)
			},
			new PointFloat
			{
				X = int.Parse(textBox_leftbottom_x.Text),
				Y = int.Parse(textBox_leftbottom_y.Text)
			}
		});
		base.DialogResult = DialogResult.OK;
	}

	private void button_close_Click(object sender, EventArgs e)
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
		this.label1 = new System.Windows.Forms.Label();
		this.label2 = new System.Windows.Forms.Label();
		this.label3 = new System.Windows.Forms.Label();
		this.label4 = new System.Windows.Forms.Label();
		this.textBox_lefttop_x = new System.Windows.Forms.TextBox();
		this.textBox_lefttop_y = new System.Windows.Forms.TextBox();
		this.textBox_righttop_x = new System.Windows.Forms.TextBox();
		this.textBox_righttop_y = new System.Windows.Forms.TextBox();
		this.textBox_leftbottom_x = new System.Windows.Forms.TextBox();
		this.textBox_leftbottom_y = new System.Windows.Forms.TextBox();
		this.textBox_rightbottom_x = new System.Windows.Forms.TextBox();
		this.textBox_rightbottom_y = new System.Windows.Forms.TextBox();
		this.button_ok = new System.Windows.Forms.Button();
		this.button_close = new System.Windows.Forms.Button();
		base.SuspendLayout();
		this.label1.AutoSize = true;
		this.label1.Location = new System.Drawing.Point(12, 9);
		this.label1.Name = "label1";
		this.label1.Size = new System.Drawing.Size(29, 12);
		this.label1.TabIndex = 0;
		this.label1.Text = "左上";
		this.label2.AutoSize = true;
		this.label2.Location = new System.Drawing.Point(12, 41);
		this.label2.Name = "label2";
		this.label2.Size = new System.Drawing.Size(29, 12);
		this.label2.TabIndex = 0;
		this.label2.Text = "右上";
		this.label3.AutoSize = true;
		this.label3.Location = new System.Drawing.Point(12, 106);
		this.label3.Name = "label3";
		this.label3.Size = new System.Drawing.Size(29, 12);
		this.label3.TabIndex = 0;
		this.label3.Text = "左下";
		this.label4.AutoSize = true;
		this.label4.Location = new System.Drawing.Point(13, 77);
		this.label4.Name = "label4";
		this.label4.Size = new System.Drawing.Size(29, 12);
		this.label4.TabIndex = 0;
		this.label4.Text = "右下";
		this.textBox_lefttop_x.Location = new System.Drawing.Point(56, 6);
		this.textBox_lefttop_x.Name = "textBox_lefttop_x";
		this.textBox_lefttop_x.Size = new System.Drawing.Size(100, 21);
		this.textBox_lefttop_x.TabIndex = 1;
		this.textBox_lefttop_x.Text = "0";
		this.textBox_lefttop_y.Location = new System.Drawing.Point(187, 6);
		this.textBox_lefttop_y.Name = "textBox_lefttop_y";
		this.textBox_lefttop_y.Size = new System.Drawing.Size(100, 21);
		this.textBox_lefttop_y.TabIndex = 1;
		this.textBox_lefttop_y.Text = "0";
		this.textBox_righttop_x.Location = new System.Drawing.Point(56, 38);
		this.textBox_righttop_x.Name = "textBox_righttop_x";
		this.textBox_righttop_x.Size = new System.Drawing.Size(100, 21);
		this.textBox_righttop_x.TabIndex = 1;
		this.textBox_righttop_x.Text = "0";
		this.textBox_righttop_y.Location = new System.Drawing.Point(187, 38);
		this.textBox_righttop_y.Name = "textBox_righttop_y";
		this.textBox_righttop_y.Size = new System.Drawing.Size(100, 21);
		this.textBox_righttop_y.TabIndex = 1;
		this.textBox_righttop_y.Text = "0";
		this.textBox_leftbottom_x.Location = new System.Drawing.Point(56, 103);
		this.textBox_leftbottom_x.Name = "textBox_leftbottom_x";
		this.textBox_leftbottom_x.Size = new System.Drawing.Size(100, 21);
		this.textBox_leftbottom_x.TabIndex = 1;
		this.textBox_leftbottom_x.Text = "0";
		this.textBox_leftbottom_y.Location = new System.Drawing.Point(187, 103);
		this.textBox_leftbottom_y.Name = "textBox_leftbottom_y";
		this.textBox_leftbottom_y.Size = new System.Drawing.Size(100, 21);
		this.textBox_leftbottom_y.TabIndex = 1;
		this.textBox_leftbottom_y.Text = "0";
		this.textBox_rightbottom_x.Location = new System.Drawing.Point(56, 70);
		this.textBox_rightbottom_x.Name = "textBox_rightbottom_x";
		this.textBox_rightbottom_x.Size = new System.Drawing.Size(100, 21);
		this.textBox_rightbottom_x.TabIndex = 1;
		this.textBox_rightbottom_x.Text = "0";
		this.textBox_rightbottom_y.Location = new System.Drawing.Point(187, 70);
		this.textBox_rightbottom_y.Name = "textBox_rightbottom_y";
		this.textBox_rightbottom_y.Size = new System.Drawing.Size(100, 21);
		this.textBox_rightbottom_y.TabIndex = 1;
		this.textBox_rightbottom_y.Text = "0";
		this.button_ok.Location = new System.Drawing.Point(71, 138);
		this.button_ok.Name = "button_ok";
		this.button_ok.Size = new System.Drawing.Size(75, 23);
		this.button_ok.TabIndex = 2;
		this.button_ok.Text = "确定";
		this.button_ok.UseVisualStyleBackColor = true;
		this.button_ok.Click += new System.EventHandler(button_ok_Click);
		this.button_close.Location = new System.Drawing.Point(167, 138);
		this.button_close.Name = "button_close";
		this.button_close.Size = new System.Drawing.Size(75, 23);
		this.button_close.TabIndex = 3;
		this.button_close.Text = "取消";
		this.button_close.UseVisualStyleBackColor = true;
		this.button_close.Click += new System.EventHandler(button_close_Click);
		base.AutoScaleDimensions = new System.Drawing.SizeF(6f, 12f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(329, 169);
		base.Controls.Add(this.button_close);
		base.Controls.Add(this.button_ok);
		base.Controls.Add(this.textBox_rightbottom_y);
		base.Controls.Add(this.textBox_leftbottom_y);
		base.Controls.Add(this.textBox_righttop_y);
		base.Controls.Add(this.textBox_lefttop_y);
		base.Controls.Add(this.textBox_rightbottom_x);
		base.Controls.Add(this.textBox_leftbottom_x);
		base.Controls.Add(this.textBox_righttop_x);
		base.Controls.Add(this.textBox_lefttop_x);
		base.Controls.Add(this.label4);
		base.Controls.Add(this.label2);
		base.Controls.Add(this.label3);
		base.Controls.Add(this.label1);
		base.MaximizeBox = false;
		base.MinimizeBox = false;
		base.Name = "FormOLEDNodeProperty";
		base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
		this.Text = "FormOLEDNodeProperty";
		base.Load += new System.EventHandler(FormOLEDNodeProperty_Load);
		base.ResumeLayout(false);
		base.PerformLayout();
	}
}
