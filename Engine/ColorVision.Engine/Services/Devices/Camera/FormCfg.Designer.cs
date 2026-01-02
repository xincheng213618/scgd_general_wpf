namespace WindowsFormsTest
{
    partial class FormCfg
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.panel1 = new System.Windows.Forms.Panel();
            this.btn_apply = new System.Windows.Forms.Button();
            this.btn_Generate = new System.Windows.Forms.Button();
            this.btn_Initial = new System.Windows.Forms.Button();
            this.btn_Save = new System.Windows.Forms.Button();
            this.proGridcamera = new System.Windows.Forms.PropertyGrid();
            this.richTextBox1 = new System.Windows.Forms.RichTextBox();
            this.button1 = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.proGridexpTime = new System.Windows.Forms.PropertyGrid();
            this.button2 = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.button3 = new System.Windows.Forms.Button();
            this.label5 = new System.Windows.Forms.Label();
            this.button4 = new System.Windows.Forms.Button();
            this.label6 = new System.Windows.Forms.Label();
            this.radioBtn_old = new System.Windows.Forms.RadioButton();
            this.radioBtn_V1 = new System.Windows.Forms.RadioButton();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.btn_apply);
            this.panel1.Controls.Add(this.btn_Generate);
            this.panel1.Controls.Add(this.btn_Initial);
            this.panel1.Controls.Add(this.btn_Save);
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(871, 42);
            this.panel1.TabIndex = 1;
            // 
            // btn_apply
            // 
            this.btn_apply.Location = new System.Drawing.Point(192, 12);
            this.btn_apply.Name = "btn_apply";
            this.btn_apply.Size = new System.Drawing.Size(75, 23);
            this.btn_apply.TabIndex = 3;
            this.btn_apply.Text = "应用";
            this.btn_apply.UseVisualStyleBackColor = true;
            this.btn_apply.Click += new System.EventHandler(this.btn_apply_Click);
            // 
            // btn_Generate
            // 
            this.btn_Generate.Location = new System.Drawing.Point(102, 12);
            this.btn_Generate.Name = "btn_Generate";
            this.btn_Generate.Size = new System.Drawing.Size(75, 23);
            this.btn_Generate.TabIndex = 2;
            this.btn_Generate.Text = "生成";
            this.btn_Generate.UseVisualStyleBackColor = true;
            this.btn_Generate.Click += new System.EventHandler(this.btn_Generate_Click);
            // 
            // btn_Initial
            // 
            this.btn_Initial.Location = new System.Drawing.Point(12, 12);
            this.btn_Initial.Name = "btn_Initial";
            this.btn_Initial.Size = new System.Drawing.Size(75, 23);
            this.btn_Initial.TabIndex = 1;
            this.btn_Initial.Text = "初始化";
            this.btn_Initial.UseVisualStyleBackColor = true;
            this.btn_Initial.Click += new System.EventHandler(this.btn_Initial_Click);
            // 
            // btn_Save
            // 
            this.btn_Save.Location = new System.Drawing.Point(285, 12);
            this.btn_Save.Name = "btn_Save";
            this.btn_Save.Size = new System.Drawing.Size(75, 23);
            this.btn_Save.TabIndex = 0;
            this.btn_Save.Text = "保存";
            this.btn_Save.UseVisualStyleBackColor = true;
            this.btn_Save.Click += new System.EventHandler(this.btn_Save_Click);
            // 
            // proGridcamera
            // 
            this.proGridcamera.HelpVisible = false;
            this.proGridcamera.LineColor = System.Drawing.SystemColors.ControlDarkDark;
            this.proGridcamera.Location = new System.Drawing.Point(12, 84);
            this.proGridcamera.Name = "proGridcamera";
            this.proGridcamera.Size = new System.Drawing.Size(339, 255);
            this.proGridcamera.TabIndex = 2;
            this.proGridcamera.ToolbarVisible = false;
            // 
            // richTextBox1
            // 
            this.richTextBox1.Location = new System.Drawing.Point(379, 56);
            this.richTextBox1.Name = "richTextBox1";
            this.richTextBox1.Size = new System.Drawing.Size(480, 684);
            this.richTextBox1.TabIndex = 0;
            this.richTextBox1.Text = "";
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(12, 721);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(160, 23);
            this.button1.TabIndex = 4;
            this.button1.Text = "配置";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(14, 59);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(77, 12);
            this.label2.TabIndex = 5;
            this.label2.Text = "相机参数配置";
            // 
            // proGridexpTime
            // 
            this.proGridexpTime.HelpVisible = false;
            this.proGridexpTime.LineColor = System.Drawing.SystemColors.ControlDarkDark;
            this.proGridexpTime.Location = new System.Drawing.Point(12, 375);
            this.proGridexpTime.Name = "proGridexpTime";
            this.proGridexpTime.Size = new System.Drawing.Size(339, 196);
            this.proGridexpTime.TabIndex = 6;
            this.proGridexpTime.ToolbarVisible = false;
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(12, 611);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(336, 23);
            this.button2.TabIndex = 8;
            this.button2.Text = "配置";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(10, 585);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(77, 12);
            this.label3.TabIndex = 7;
            this.label3.Text = "配置通道设置";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(14, 351);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(101, 12);
            this.label4.TabIndex = 9;
            this.label4.Text = "自动曝光参数配置";
            // 
            // button3
            // 
            this.button3.Location = new System.Drawing.Point(9, 663);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(336, 23);
            this.button3.TabIndex = 11;
            this.button3.Text = "配置";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(10, 637);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(119, 12);
            this.label5.TabIndex = 10;
            this.label5.Text = "sys配置校正文件设置";
            // 
            // button4
            // 
            this.button4.Location = new System.Drawing.Point(188, 721);
            this.button4.Name = "button4";
            this.button4.Size = new System.Drawing.Size(157, 23);
            this.button4.TabIndex = 13;
            this.button4.Text = "配置V1";
            this.button4.UseVisualStyleBackColor = true;
            this.button4.Click += new System.EventHandler(this.button4_Click);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(10, 695);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(101, 12);
            this.label6.TabIndex = 12;
            this.label6.Text = "配置校正文件设置";
            // 
            // radioBtn_old
            // 
            this.radioBtn_old.AutoSize = true;
            this.radioBtn_old.Location = new System.Drawing.Point(150, 694);
            this.radioBtn_old.Name = "radioBtn_old";
            this.radioBtn_old.Size = new System.Drawing.Size(41, 16);
            this.radioBtn_old.TabIndex = 14;
            this.radioBtn_old.TabStop = true;
            this.radioBtn_old.Text = "Old";
            this.radioBtn_old.UseVisualStyleBackColor = true;
            this.radioBtn_old.CheckedChanged += new System.EventHandler(this.radioBtn_old_CheckedChanged);
            // 
            // radioBtn_V1
            // 
            this.radioBtn_V1.AutoSize = true;
            this.radioBtn_V1.Location = new System.Drawing.Point(250, 693);
            this.radioBtn_V1.Name = "radioBtn_V1";
            this.radioBtn_V1.Size = new System.Drawing.Size(35, 16);
            this.radioBtn_V1.TabIndex = 15;
            this.radioBtn_V1.TabStop = true;
            this.radioBtn_V1.Text = "V1";
            this.radioBtn_V1.UseVisualStyleBackColor = true;
            this.radioBtn_V1.CheckedChanged += new System.EventHandler(this.radioBtn_V1_CheckedChanged);
            // 
            // FormCfg
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(871, 756);
            this.Controls.Add(this.radioBtn_V1);
            this.Controls.Add(this.radioBtn_old);
            this.Controls.Add(this.button4);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.button3);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.proGridexpTime);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.proGridcamera);
            this.Controls.Add(this.richTextBox1);
            this.Controls.Add(this.panel1);
            this.Name = "FormCfg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "FormCfg";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.FormCfg_FormClosed);
            this.Load += new System.EventHandler(this.FormCfg_Load);
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button btn_Save;
        private System.Windows.Forms.Button btn_Initial;
        private System.Windows.Forms.Button btn_Generate;
        private System.Windows.Forms.PropertyGrid proGridcamera;
        private System.Windows.Forms.RichTextBox richTextBox1;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.PropertyGrid proGridexpTime;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button btn_apply;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Button button4;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.RadioButton radioBtn_old;
        private System.Windows.Forms.RadioButton radioBtn_V1;
    }
}