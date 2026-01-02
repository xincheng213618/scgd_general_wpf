
namespace WindowsFormsTest
{
    partial class FormChannels
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
            this.gb_Channel1 = new System.Windows.Forms.GroupBox();
            this.comboBox1 = new System.Windows.Forms.ComboBox();
            this.tb_Channel1cfwPort = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.tb_Channel1Title = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.gb_Channel3 = new System.Windows.Forms.GroupBox();
            this.comboBox3 = new System.Windows.Forms.ComboBox();
            this.tb_Channel3cfwPort = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.tb_Channel3Title = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.gb_Channel2 = new System.Windows.Forms.GroupBox();
            this.comboBox2 = new System.Windows.Forms.ComboBox();
            this.tb_Channel2cfwPort = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.tb_Channel2Title = new System.Windows.Forms.TextBox();
            this.label9 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.btn_Confirm = new System.Windows.Forms.Button();
            this.button1 = new System.Windows.Forms.Button();
            this.gb_Channel1.SuspendLayout();
            this.gb_Channel3.SuspendLayout();
            this.gb_Channel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // gb_Channel1
            // 
            this.gb_Channel1.Controls.Add(this.comboBox1);
            this.gb_Channel1.Controls.Add(this.tb_Channel1cfwPort);
            this.gb_Channel1.Controls.Add(this.label3);
            this.gb_Channel1.Controls.Add(this.tb_Channel1Title);
            this.gb_Channel1.Controls.Add(this.label2);
            this.gb_Channel1.Controls.Add(this.label1);
            this.gb_Channel1.Location = new System.Drawing.Point(12, 12);
            this.gb_Channel1.Name = "gb_Channel1";
            this.gb_Channel1.Size = new System.Drawing.Size(149, 111);
            this.gb_Channel1.TabIndex = 0;
            this.gb_Channel1.TabStop = false;
            this.gb_Channel1.Text = "通道 0";
            // 
            // comboBox1
            // 
            this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Items.AddRange(new object[] {
            "Channel_X",
            "Channel_Y",
            "Channel_Z"});
            this.comboBox1.Location = new System.Drawing.Point(53, 75);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new System.Drawing.Size(85, 20);
            this.comboBox1.TabIndex = 7;
            this.comboBox1.SelectedIndexChanged += new System.EventHandler(this.comboBox1_SelectedIndexChanged);
            // 
            // tb_Channel1cfwPort
            // 
            this.tb_Channel1cfwPort.Location = new System.Drawing.Point(64, 17);
            this.tb_Channel1cfwPort.Name = "tb_Channel1cfwPort";
            this.tb_Channel1cfwPort.Size = new System.Drawing.Size(74, 21);
            this.tb_Channel1cfwPort.TabIndex = 6;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(6, 80);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(41, 12);
            this.label3.TabIndex = 4;
            this.label3.Text = "chType";
            // 
            // tb_Channel1Title
            // 
            this.tb_Channel1Title.Location = new System.Drawing.Point(64, 46);
            this.tb_Channel1Title.Name = "tb_Channel1Title";
            this.tb_Channel1Title.Size = new System.Drawing.Size(74, 21);
            this.tb_Channel1Title.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(6, 26);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(47, 12);
            this.label2.TabIndex = 2;
            this.label2.Text = "cfwport";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(6, 53);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(35, 12);
            this.label1.TabIndex = 0;
            this.label1.Text = "Title";
            // 
            // gb_Channel3
            // 
            this.gb_Channel3.Controls.Add(this.comboBox3);
            this.gb_Channel3.Controls.Add(this.tb_Channel3cfwPort);
            this.gb_Channel3.Controls.Add(this.label4);
            this.gb_Channel3.Controls.Add(this.tb_Channel3Title);
            this.gb_Channel3.Controls.Add(this.label6);
            this.gb_Channel3.Controls.Add(this.label5);
            this.gb_Channel3.Location = new System.Drawing.Point(335, 12);
            this.gb_Channel3.Name = "gb_Channel3";
            this.gb_Channel3.Size = new System.Drawing.Size(149, 111);
            this.gb_Channel3.TabIndex = 7;
            this.gb_Channel3.TabStop = false;
            this.gb_Channel3.Text = "通道 2";
            // 
            // comboBox3
            // 
            this.comboBox3.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox3.FormattingEnabled = true;
            this.comboBox3.Items.AddRange(new object[] {
            "Channel_X",
            "Channel_Y",
            "Channel_Z"});
            this.comboBox3.Location = new System.Drawing.Point(52, 76);
            this.comboBox3.Name = "comboBox3";
            this.comboBox3.Size = new System.Drawing.Size(85, 20);
            this.comboBox3.TabIndex = 9;
            this.comboBox3.SelectedIndexChanged += new System.EventHandler(this.comboBox3_SelectedIndexChanged);
            // 
            // tb_Channel3cfwPort
            // 
            this.tb_Channel3cfwPort.Location = new System.Drawing.Point(63, 17);
            this.tb_Channel3cfwPort.Name = "tb_Channel3cfwPort";
            this.tb_Channel3cfwPort.Size = new System.Drawing.Size(74, 21);
            this.tb_Channel3cfwPort.TabIndex = 6;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(7, 80);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(41, 12);
            this.label4.TabIndex = 4;
            this.label4.Text = "chType";
            // 
            // tb_Channel3Title
            // 
            this.tb_Channel3Title.Location = new System.Drawing.Point(63, 47);
            this.tb_Channel3Title.Name = "tb_Channel3Title";
            this.tb_Channel3Title.Size = new System.Drawing.Size(74, 21);
            this.tb_Channel3Title.TabIndex = 1;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(7, 51);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(35, 12);
            this.label6.TabIndex = 0;
            this.label6.Text = "Title";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(7, 22);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(47, 12);
            this.label5.TabIndex = 2;
            this.label5.Text = "cfwport";
            // 
            // gb_Channel2
            // 
            this.gb_Channel2.Controls.Add(this.comboBox2);
            this.gb_Channel2.Controls.Add(this.tb_Channel2cfwPort);
            this.gb_Channel2.Controls.Add(this.label7);
            this.gb_Channel2.Controls.Add(this.tb_Channel2Title);
            this.gb_Channel2.Controls.Add(this.label9);
            this.gb_Channel2.Controls.Add(this.label8);
            this.gb_Channel2.Location = new System.Drawing.Point(172, 12);
            this.gb_Channel2.Name = "gb_Channel2";
            this.gb_Channel2.Size = new System.Drawing.Size(149, 111);
            this.gb_Channel2.TabIndex = 0;
            this.gb_Channel2.TabStop = false;
            this.gb_Channel2.Text = "通道 1";
            // 
            // comboBox2
            // 
            this.comboBox2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox2.FormattingEnabled = true;
            this.comboBox2.Items.AddRange(new object[] {
            "Channel_X",
            "Channel_Y",
            "Channel_Z"});
            this.comboBox2.Location = new System.Drawing.Point(53, 72);
            this.comboBox2.Name = "comboBox2";
            this.comboBox2.Size = new System.Drawing.Size(85, 20);
            this.comboBox2.TabIndex = 8;
            this.comboBox2.SelectedIndexChanged += new System.EventHandler(this.comboBox2_SelectedIndexChanged);
            // 
            // tb_Channel2cfwPort
            // 
            this.tb_Channel2cfwPort.Location = new System.Drawing.Point(64, 13);
            this.tb_Channel2cfwPort.Name = "tb_Channel2cfwPort";
            this.tb_Channel2cfwPort.Size = new System.Drawing.Size(74, 21);
            this.tb_Channel2cfwPort.TabIndex = 6;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(7, 80);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(41, 12);
            this.label7.TabIndex = 4;
            this.label7.Text = "chType";
            // 
            // tb_Channel2Title
            // 
            this.tb_Channel2Title.Location = new System.Drawing.Point(64, 44);
            this.tb_Channel2Title.Name = "tb_Channel2Title";
            this.tb_Channel2Title.Size = new System.Drawing.Size(74, 21);
            this.tb_Channel2Title.TabIndex = 1;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(7, 51);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(35, 12);
            this.label9.TabIndex = 0;
            this.label9.Text = "Title";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(7, 22);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(47, 12);
            this.label8.TabIndex = 2;
            this.label8.Text = "cfwport";
            // 
            // btn_Confirm
            // 
            this.btn_Confirm.Location = new System.Drawing.Point(141, 137);
            this.btn_Confirm.Name = "btn_Confirm";
            this.btn_Confirm.Size = new System.Drawing.Size(75, 23);
            this.btn_Confirm.TabIndex = 8;
            this.btn_Confirm.Text = "确定";
            this.btn_Confirm.UseVisualStyleBackColor = true;
            this.btn_Confirm.Click += new System.EventHandler(this.btn_Confirm_Click);
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(269, 137);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 13;
            this.button1.Text = "取消";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // FormChannels
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(494, 168);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.gb_Channel3);
            this.Controls.Add(this.btn_Confirm);
            this.Controls.Add(this.gb_Channel2);
            this.Controls.Add(this.gb_Channel1);
            this.Name = "FormChannels";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "FormChannels";
            this.Load += new System.EventHandler(this.FormChannels_Load);
            this.gb_Channel1.ResumeLayout(false);
            this.gb_Channel1.PerformLayout();
            this.gb_Channel3.ResumeLayout(false);
            this.gb_Channel3.PerformLayout();
            this.gb_Channel2.ResumeLayout(false);
            this.gb_Channel2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox gb_Channel1;
        private System.Windows.Forms.TextBox tb_Channel1Title;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox tb_Channel1cfwPort;
        private System.Windows.Forms.GroupBox gb_Channel3;
        private System.Windows.Forms.TextBox tb_Channel3cfwPort;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox tb_Channel3Title;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.GroupBox gb_Channel2;
        private System.Windows.Forms.TextBox tb_Channel2cfwPort;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox tb_Channel2Title;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.ComboBox comboBox1;
        private System.Windows.Forms.ComboBox comboBox3;
        private System.Windows.Forms.ComboBox comboBox2;
        private System.Windows.Forms.Button btn_Confirm;
        private System.Windows.Forms.Button button1;
    }
}