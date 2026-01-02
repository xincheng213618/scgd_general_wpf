namespace WindowsFormsTest
{
    partial class Form1
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.btn_Connect = new System.Windows.Forms.Button();
            this.btn_MeasTif = new System.Windows.Forms.Button();
            this.panel1 = new System.Windows.Forms.Panel();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.panel2 = new System.Windows.Forms.Panel();
            this.cb_CM_MODE = new System.Windows.Forms.ComboBox();
            this.label15 = new System.Windows.Forms.Label();
            this.btn_reset = new System.Windows.Forms.Button();
            this.cb_DSNU = new System.Windows.Forms.CheckBox();
            this.label16 = new System.Windows.Forms.Label();
            this.tb_Exp3 = new System.Windows.Forms.TextBox();
            this.label14 = new System.Windows.Forms.Label();
            this.tb_Exp2 = new System.Windows.Forms.TextBox();
            this.button2 = new System.Windows.Forms.Button();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.btn_Distortion = new System.Windows.Forms.Button();
            this.btn_SFR = new System.Windows.Forms.Button();
            this.btn_FOV = new System.Windows.Forms.Button();
            this.label13 = new System.Windows.Forms.Label();
            this.button1 = new System.Windows.Forms.Button();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.cb_CM_ID = new System.Windows.Forms.ComboBox();
            this.label12 = new System.Windows.Forms.Label();
            this.cb_bpp = new System.Windows.Forms.ComboBox();
            this.label11 = new System.Windows.Forms.Label();
            this.cb_get_mode = new System.Windows.Forms.ComboBox();
            this.label10 = new System.Windows.Forms.Label();
            this.cb_LumCorrect = new System.Windows.Forms.CheckBox();
            this.cb_MonoCorrect = new System.Windows.Forms.CheckBox();
            this.cb_Channels = new System.Windows.Forms.ComboBox();
            this.label9 = new System.Windows.Forms.Label();
            this.cb_CM_TYPE = new System.Windows.Forms.ComboBox();
            this.label8 = new System.Windows.Forms.Label();
            this.btn_Test = new System.Windows.Forms.Button();
            this.cb_MulColorCorrect = new System.Windows.Forms.CheckBox();
            this.btn_SetGain = new System.Windows.Forms.Button();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.tb_Gain = new System.Windows.Forms.TextBox();
            this.tb_p = new System.Windows.Forms.TextBox();
            this.tb_e = new System.Windows.Forms.TextBox();
            this.tb_s = new System.Windows.Forms.TextBox();
            this.btn_SetExp = new System.Windows.Forms.Button();
            this.btn_CalAutoExp = new System.Windows.Forms.Button();
            this.cb_DistortCorrect = new System.Windows.Forms.CheckBox();
            this.cb_UniformFieldCorrect = new System.Windows.Forms.CheckBox();
            this.cb_BadPixelCorrect = new System.Windows.Forms.CheckBox();
            this.cb_FourColorCorrect = new System.Windows.Forms.CheckBox();
            this.cb_DarkNoiseCorrect = new System.Windows.Forms.CheckBox();
            this.cb_AutoExp = new System.Windows.Forms.CheckBox();
            this.label5 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.tb_TiffName = new System.Windows.Forms.TextBox();
            this.tb_TiffPath = new System.Windows.Forms.TextBox();
            this.tb_Exp = new System.Windows.Forms.TextBox();
            this.tb_Count = new System.Windows.Forms.TextBox();
            this.btn_ConfigFile = new System.Windows.Forms.Button();
            this.btn_close = new System.Windows.Forms.Button();
            this.btn_Meas = new System.Windows.Forms.Button();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.显示ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.MenuItem_ShowCenterLine = new System.Windows.Forms.ToolStripMenuItem();
            this.基本参数设置ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.Camera_MenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.Channels_MenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.Calibration_MenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ExpTime_MenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timer2 = new System.Windows.Forms.Timer(this.components);
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.panel2.SuspendLayout();
            this.menuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // btn_Connect
            // 
            this.btn_Connect.Location = new System.Drawing.Point(110, 267);
            this.btn_Connect.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.btn_Connect.Name = "btn_Connect";
            this.btn_Connect.Size = new System.Drawing.Size(170, 39);
            this.btn_Connect.TabIndex = 0;
            this.btn_Connect.Text = "连接";
            this.btn_Connect.UseVisualStyleBackColor = true;
            this.btn_Connect.Click += new System.EventHandler(this.btn_MeasConnect_Click);
            // 
            // btn_MeasTif
            // 
            this.btn_MeasTif.Location = new System.Drawing.Point(110, 449);
            this.btn_MeasTif.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.btn_MeasTif.Name = "btn_MeasTif";
            this.btn_MeasTif.Size = new System.Drawing.Size(170, 39);
            this.btn_MeasTif.TabIndex = 1;
            this.btn_MeasTif.Text = "测量tif";
            this.btn_MeasTif.UseVisualStyleBackColor = true;
            this.btn_MeasTif.Click += new System.EventHandler(this.btn_MeasTif_Click);
            // 
            // panel1
            // 
            this.panel1.AutoScroll = true;
            this.panel1.Controls.Add(this.pictureBox1);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(448, 38);
            this.panel1.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1701, 1415);
            this.panel1.TabIndex = 2;
            // 
            // pictureBox1
            // 
            this.pictureBox1.BackColor = System.Drawing.SystemColors.ActiveBorder;
            this.pictureBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pictureBox1.Location = new System.Drawing.Point(0, 0);
            this.pictureBox1.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(1701, 1415);
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
            this.pictureBox1.Paint += new System.Windows.Forms.PaintEventHandler(this.pictureBox1_Paint);
            this.pictureBox1.MouseDown += new System.Windows.Forms.MouseEventHandler(this.pictureBox1_MouseDown);
            this.pictureBox1.MouseEnter += new System.EventHandler(this.pictureBox1_MouseEnter);
            this.pictureBox1.MouseLeave += new System.EventHandler(this.pictureBox1_MouseLeave);
            this.pictureBox1.MouseMove += new System.Windows.Forms.MouseEventHandler(this.pictureBox1_MouseMove);
            this.pictureBox1.MouseUp += new System.Windows.Forms.MouseEventHandler(this.pictureBox1_MouseUp);
            this.pictureBox1.MouseWheel += new System.Windows.Forms.MouseEventHandler(this.pictureBox1_MouseWheel);
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.cb_CM_MODE);
            this.panel2.Controls.Add(this.label15);
            this.panel2.Controls.Add(this.btn_reset);
            this.panel2.Controls.Add(this.cb_DSNU);
            this.panel2.Controls.Add(this.label16);
            this.panel2.Controls.Add(this.tb_Exp3);
            this.panel2.Controls.Add(this.label14);
            this.panel2.Controls.Add(this.tb_Exp2);
            this.panel2.Controls.Add(this.button2);
            this.panel2.Controls.Add(this.checkBox1);
            this.panel2.Controls.Add(this.btn_Distortion);
            this.panel2.Controls.Add(this.btn_SFR);
            this.panel2.Controls.Add(this.btn_FOV);
            this.panel2.Controls.Add(this.label13);
            this.panel2.Controls.Add(this.button1);
            this.panel2.Controls.Add(this.textBox1);
            this.panel2.Controls.Add(this.cb_CM_ID);
            this.panel2.Controls.Add(this.label12);
            this.panel2.Controls.Add(this.cb_bpp);
            this.panel2.Controls.Add(this.label11);
            this.panel2.Controls.Add(this.cb_get_mode);
            this.panel2.Controls.Add(this.label10);
            this.panel2.Controls.Add(this.cb_LumCorrect);
            this.panel2.Controls.Add(this.cb_MonoCorrect);
            this.panel2.Controls.Add(this.cb_Channels);
            this.panel2.Controls.Add(this.label9);
            this.panel2.Controls.Add(this.cb_CM_TYPE);
            this.panel2.Controls.Add(this.label8);
            this.panel2.Controls.Add(this.btn_Test);
            this.panel2.Controls.Add(this.cb_MulColorCorrect);
            this.panel2.Controls.Add(this.btn_SetGain);
            this.panel2.Controls.Add(this.label6);
            this.panel2.Controls.Add(this.label7);
            this.panel2.Controls.Add(this.tb_Gain);
            this.panel2.Controls.Add(this.tb_p);
            this.panel2.Controls.Add(this.tb_e);
            this.panel2.Controls.Add(this.tb_s);
            this.panel2.Controls.Add(this.btn_SetExp);
            this.panel2.Controls.Add(this.btn_CalAutoExp);
            this.panel2.Controls.Add(this.cb_DistortCorrect);
            this.panel2.Controls.Add(this.cb_UniformFieldCorrect);
            this.panel2.Controls.Add(this.cb_BadPixelCorrect);
            this.panel2.Controls.Add(this.cb_FourColorCorrect);
            this.panel2.Controls.Add(this.cb_DarkNoiseCorrect);
            this.panel2.Controls.Add(this.cb_AutoExp);
            this.panel2.Controls.Add(this.label5);
            this.panel2.Controls.Add(this.label2);
            this.panel2.Controls.Add(this.label4);
            this.panel2.Controls.Add(this.label3);
            this.panel2.Controls.Add(this.label1);
            this.panel2.Controls.Add(this.tb_TiffName);
            this.panel2.Controls.Add(this.tb_TiffPath);
            this.panel2.Controls.Add(this.tb_Exp);
            this.panel2.Controls.Add(this.tb_Count);
            this.panel2.Controls.Add(this.btn_ConfigFile);
            this.panel2.Controls.Add(this.btn_close);
            this.panel2.Controls.Add(this.btn_Connect);
            this.panel2.Controls.Add(this.btn_Meas);
            this.panel2.Controls.Add(this.btn_MeasTif);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Left;
            this.panel2.Location = new System.Drawing.Point(0, 38);
            this.panel2.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(448, 1415);
            this.panel2.TabIndex = 3;
            // 
            // cb_CM_MODE
            // 
            this.cb_CM_MODE.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cb_CM_MODE.FormattingEnabled = true;
            this.cb_CM_MODE.Items.AddRange(new object[] {
            "CV_MODE",
            "BV_MODE",
            "LV_MODE",
            "LVTOBV_MODE"});
            this.cb_CM_MODE.Location = new System.Drawing.Point(188, 53);
            this.cb_CM_MODE.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.cb_CM_MODE.Name = "cb_CM_MODE";
            this.cb_CM_MODE.Size = new System.Drawing.Size(168, 29);
            this.cb_CM_MODE.TabIndex = 169;
            this.cb_CM_MODE.SelectedIndexChanged += new System.EventHandler(this.cb_CM_MODE_SelectedIndexChanged);
            // 
            // label15
            // 
            this.label15.AutoSize = true;
            this.label15.Location = new System.Drawing.Point(55, 63);
            this.label15.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(115, 21);
            this.label15.TabIndex = 168;
            this.label15.Text = "相机模式：";
            // 
            // btn_reset
            // 
            this.btn_reset.Location = new System.Drawing.Point(110, 311);
            this.btn_reset.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.btn_reset.Name = "btn_reset";
            this.btn_reset.Size = new System.Drawing.Size(170, 39);
            this.btn_reset.TabIndex = 167;
            this.btn_reset.Text = "复位";
            this.btn_reset.UseVisualStyleBackColor = true;
            this.btn_reset.Click += new System.EventHandler(this.btn_reset_Click);
            // 
            // cb_DSNU
            // 
            this.cb_DSNU.AutoSize = true;
            this.cb_DSNU.Location = new System.Drawing.Point(59, 942);
            this.cb_DSNU.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.cb_DSNU.Name = "cb_DSNU";
            this.cb_DSNU.Size = new System.Drawing.Size(80, 25);
            this.cb_DSNU.TabIndex = 166;
            this.cb_DSNU.Text = "DSNU";
            this.cb_DSNU.UseVisualStyleBackColor = true;
            // 
            // label16
            // 
            this.label16.AutoSize = true;
            this.label16.Location = new System.Drawing.Point(239, 909);
            this.label16.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label16.Name = "label16";
            this.label16.Size = new System.Drawing.Size(32, 21);
            this.label16.TabIndex = 165;
            this.label16.Text = "ms";
            // 
            // tb_Exp3
            // 
            this.tb_Exp3.Location = new System.Drawing.Point(139, 895);
            this.tb_Exp3.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.tb_Exp3.Name = "tb_Exp3";
            this.tb_Exp3.Size = new System.Drawing.Size(88, 31);
            this.tb_Exp3.TabIndex = 164;
            this.tb_Exp3.Text = "100";
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(239, 853);
            this.label14.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(32, 21);
            this.label14.TabIndex = 161;
            this.label14.Text = "ms";
            // 
            // tb_Exp2
            // 
            this.tb_Exp2.Location = new System.Drawing.Point(139, 846);
            this.tb_Exp2.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.tb_Exp2.Name = "tb_Exp2";
            this.tb_Exp2.Size = new System.Drawing.Size(88, 31);
            this.tb_Exp2.TabIndex = 160;
            this.tb_Exp2.Text = "100";
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(242, 1256);
            this.button2.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(138, 39);
            this.button2.TabIndex = 159;
            this.button2.Text = "显示设置";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // checkBox1
            // 
            this.checkBox1.AutoSize = true;
            this.checkBox1.Location = new System.Drawing.Point(242, 1067);
            this.checkBox1.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(78, 25);
            this.checkBox1.TabIndex = 158;
            this.checkBox1.Text = "框选";
            this.checkBox1.UseVisualStyleBackColor = true;
            this.checkBox1.CheckedChanged += new System.EventHandler(this.checkBox1_CheckedChanged);
            // 
            // btn_Distortion
            // 
            this.btn_Distortion.Location = new System.Drawing.Point(242, 1207);
            this.btn_Distortion.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.btn_Distortion.Name = "btn_Distortion";
            this.btn_Distortion.Size = new System.Drawing.Size(138, 39);
            this.btn_Distortion.TabIndex = 157;
            this.btn_Distortion.Text = "执行Disto";
            this.btn_Distortion.UseVisualStyleBackColor = true;
            this.btn_Distortion.Click += new System.EventHandler(this.btn_Distortion_Click);
            // 
            // btn_SFR
            // 
            this.btn_SFR.Location = new System.Drawing.Point(242, 1152);
            this.btn_SFR.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.btn_SFR.Name = "btn_SFR";
            this.btn_SFR.Size = new System.Drawing.Size(138, 39);
            this.btn_SFR.TabIndex = 156;
            this.btn_SFR.Text = "执行 SFR";
            this.btn_SFR.UseVisualStyleBackColor = true;
            this.btn_SFR.Click += new System.EventHandler(this.btn_SFR_Click);
            // 
            // btn_FOV
            // 
            this.btn_FOV.Location = new System.Drawing.Point(242, 1103);
            this.btn_FOV.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.btn_FOV.Name = "btn_FOV";
            this.btn_FOV.Size = new System.Drawing.Size(138, 39);
            this.btn_FOV.TabIndex = 154;
            this.btn_FOV.Text = "执行FOV";
            this.btn_FOV.UseVisualStyleBackColor = true;
            this.btn_FOV.Click += new System.EventHandler(this.btn_FOV_Click);
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(292, 557);
            this.label13.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(129, 21);
            this.label13.TabIndex = 30;
            this.label13.Text = "阈值(0-255)";
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(47, 549);
            this.button1.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(129, 39);
            this.button1.TabIndex = 29;
            this.button1.Text = "计算位置";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(198, 552);
            this.textBox1.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(70, 31);
            this.textBox1.TabIndex = 28;
            this.textBox1.Text = "30";
            // 
            // cb_CM_ID
            // 
            this.cb_CM_ID.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cb_CM_ID.FormattingEnabled = true;
            this.cb_CM_ID.Items.AddRange(new object[] {
            "CV_Q",
            "LV_Q",
            "BV_Q",
            "MIL_CL",
            "MIL_CXP",
            "BV_H",
            "LV_H",
            "HK_CXP"});
            this.cb_CM_ID.Location = new System.Drawing.Point(188, 94);
            this.cb_CM_ID.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.cb_CM_ID.Name = "cb_CM_ID";
            this.cb_CM_ID.Size = new System.Drawing.Size(168, 29);
            this.cb_CM_ID.TabIndex = 27;
            this.cb_CM_ID.SelectedIndexChanged += new System.EventHandler(this.cb_CM_ID_SelectedIndexChanged);
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(55, 102);
            this.label12.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(95, 21);
            this.label12.TabIndex = 26;
            this.label12.Text = "相机ID：";
            // 
            // cb_bpp
            // 
            this.cb_bpp.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cb_bpp.FormattingEnabled = true;
            this.cb_bpp.Items.AddRange(new object[] {
            "8",
            "16"});
            this.cb_bpp.Location = new System.Drawing.Point(188, 220);
            this.cb_bpp.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.cb_bpp.Name = "cb_bpp";
            this.cb_bpp.Size = new System.Drawing.Size(168, 29);
            this.cb_bpp.TabIndex = 25;
            this.cb_bpp.SelectedIndexChanged += new System.EventHandler(this.cb_bpp_SelectedIndexChanged);
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(55, 228);
            this.label11.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(115, 21);
            this.label11.TabIndex = 24;
            this.label11.Text = "像素位数：";
            // 
            // cb_get_mode
            // 
            this.cb_get_mode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cb_get_mode.FormattingEnabled = true;
            this.cb_get_mode.Items.AddRange(new object[] {
            "Measure_Normal",
            "Live ",
            "Measure_Fast",
            "Measure_FastEx "});
            this.cb_get_mode.Location = new System.Drawing.Point(188, 178);
            this.cb_get_mode.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.cb_get_mode.Name = "cb_get_mode";
            this.cb_get_mode.Size = new System.Drawing.Size(168, 29);
            this.cb_get_mode.TabIndex = 23;
            this.cb_get_mode.SelectedIndexChanged += new System.EventHandler(this.cb_get_mode_SelectedIndexChanged);
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(55, 186);
            this.label10.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(115, 21);
            this.label10.TabIndex = 22;
            this.label10.Text = "取图模式：";
            // 
            // cb_LumCorrect
            // 
            this.cb_LumCorrect.AutoSize = true;
            this.cb_LumCorrect.Location = new System.Drawing.Point(59, 1019);
            this.cb_LumCorrect.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.cb_LumCorrect.Name = "cb_LumCorrect";
            this.cb_LumCorrect.Size = new System.Drawing.Size(177, 25);
            this.cb_LumCorrect.TabIndex = 21;
            this.cb_LumCorrect.Text = "Luminance校正";
            this.cb_LumCorrect.UseVisualStyleBackColor = true;
            // 
            // cb_MonoCorrect
            // 
            this.cb_MonoCorrect.AutoSize = true;
            this.cb_MonoCorrect.Location = new System.Drawing.Point(59, 1060);
            this.cb_MonoCorrect.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.cb_MonoCorrect.Name = "cb_MonoCorrect";
            this.cb_MonoCorrect.Size = new System.Drawing.Size(120, 25);
            this.cb_MonoCorrect.TabIndex = 20;
            this.cb_MonoCorrect.Text = "单色校正";
            this.cb_MonoCorrect.UseVisualStyleBackColor = true;
            this.cb_MonoCorrect.CheckedChanged += new System.EventHandler(this.cb_MonoCorrect_CheckedChanged);
            // 
            // cb_Channels
            // 
            this.cb_Channels.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cb_Channels.Enabled = false;
            this.cb_Channels.FormattingEnabled = true;
            this.cb_Channels.Items.AddRange(new object[] {
            "1",
            "3"});
            this.cb_Channels.Location = new System.Drawing.Point(188, 136);
            this.cb_Channels.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.cb_Channels.Name = "cb_Channels";
            this.cb_Channels.Size = new System.Drawing.Size(168, 29);
            this.cb_Channels.TabIndex = 19;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(55, 144);
            this.label9.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(94, 21);
            this.label9.TabIndex = 18;
            this.label9.Text = "通道数：";
            // 
            // cb_CM_TYPE
            // 
            this.cb_CM_TYPE.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cb_CM_TYPE.FormattingEnabled = true;
            this.cb_CM_TYPE.Items.AddRange(new object[] {
            "QHY_USB",
            "HK_USB",
            "HK_CARD",
            "MIL_CL_CARD",
            "MIL_CXP_CARD",
            "NN_USB",
            "TOUP_USB",
            "HK_FG_CARD",
            "IKAP_CARD"});
            this.cb_CM_TYPE.Location = new System.Drawing.Point(188, 17);
            this.cb_CM_TYPE.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.cb_CM_TYPE.Name = "cb_CM_TYPE";
            this.cb_CM_TYPE.Size = new System.Drawing.Size(168, 29);
            this.cb_CM_TYPE.TabIndex = 17;
            this.cb_CM_TYPE.SelectedIndexChanged += new System.EventHandler(this.cb_CM_TYPE_SelectedIndexChanged);
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(55, 25);
            this.label8.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(115, 21);
            this.label8.TabIndex = 16;
            this.label8.Text = "相机型号：";
            // 
            // btn_Test
            // 
            this.btn_Test.Location = new System.Drawing.Point(41, 1299);
            this.btn_Test.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.btn_Test.Name = "btn_Test";
            this.btn_Test.Size = new System.Drawing.Size(170, 39);
            this.btn_Test.TabIndex = 15;
            this.btn_Test.Text = "自动对焦测试";
            this.btn_Test.UseVisualStyleBackColor = true;
            this.btn_Test.Click += new System.EventHandler(this.btn_Test_Click);
            // 
            // cb_MulColorCorrect
            // 
            this.cb_MulColorCorrect.AutoSize = true;
            this.cb_MulColorCorrect.Location = new System.Drawing.Point(59, 1137);
            this.cb_MulColorCorrect.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.cb_MulColorCorrect.Name = "cb_MulColorCorrect";
            this.cb_MulColorCorrect.Size = new System.Drawing.Size(120, 25);
            this.cb_MulColorCorrect.TabIndex = 14;
            this.cb_MulColorCorrect.Text = "多色校正";
            this.cb_MulColorCorrect.UseVisualStyleBackColor = true;
            this.cb_MulColorCorrect.CheckedChanged += new System.EventHandler(this.cb_MulColorCorrect_CheckedChanged);
            // 
            // btn_SetGain
            // 
            this.btn_SetGain.Location = new System.Drawing.Point(279, 745);
            this.btn_SetGain.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.btn_SetGain.Name = "btn_SetGain";
            this.btn_SetGain.Size = new System.Drawing.Size(73, 39);
            this.btn_SetGain.TabIndex = 13;
            this.btn_SetGain.Text = "设置";
            this.btn_SetGain.UseVisualStyleBackColor = true;
            this.btn_SetGain.Click += new System.EventHandler(this.btn_SetGain_Click);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(242, 755);
            this.label6.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(32, 21);
            this.label6.TabIndex = 11;
            this.label6.Text = "dB";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(55, 755);
            this.label7.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(52, 21);
            this.label7.TabIndex = 12;
            this.label7.Text = "增益";
            // 
            // tb_Gain
            // 
            this.tb_Gain.Location = new System.Drawing.Point(139, 748);
            this.tb_Gain.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.tb_Gain.Name = "tb_Gain";
            this.tb_Gain.Size = new System.Drawing.Size(88, 31);
            this.tb_Gain.TabIndex = 10;
            this.tb_Gain.Text = "10";
            // 
            // tb_p
            // 
            this.tb_p.Location = new System.Drawing.Point(260, 601);
            this.tb_p.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.tb_p.Name = "tb_p";
            this.tb_p.Size = new System.Drawing.Size(70, 31);
            this.tb_p.TabIndex = 9;
            this.tb_p.Text = "0";
            // 
            // tb_e
            // 
            this.tb_e.Location = new System.Drawing.Point(169, 601);
            this.tb_e.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.tb_e.Name = "tb_e";
            this.tb_e.Size = new System.Drawing.Size(70, 31);
            this.tb_e.TabIndex = 9;
            this.tb_e.Text = "3";
            // 
            // tb_s
            // 
            this.tb_s.Location = new System.Drawing.Point(78, 601);
            this.tb_s.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.tb_s.Name = "tb_s";
            this.tb_s.Size = new System.Drawing.Size(70, 31);
            this.tb_s.TabIndex = 9;
            this.tb_s.Text = "1";
            // 
            // btn_SetExp
            // 
            this.btn_SetExp.Location = new System.Drawing.Point(280, 797);
            this.btn_SetExp.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.btn_SetExp.Name = "btn_SetExp";
            this.btn_SetExp.Size = new System.Drawing.Size(73, 39);
            this.btn_SetExp.TabIndex = 8;
            this.btn_SetExp.Text = "设置";
            this.btn_SetExp.UseVisualStyleBackColor = true;
            this.btn_SetExp.Click += new System.EventHandler(this.btn_SetExp_Click);
            // 
            // btn_CalAutoExp
            // 
            this.btn_CalAutoExp.Location = new System.Drawing.Point(110, 493);
            this.btn_CalAutoExp.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.btn_CalAutoExp.Name = "btn_CalAutoExp";
            this.btn_CalAutoExp.Size = new System.Drawing.Size(170, 39);
            this.btn_CalAutoExp.TabIndex = 7;
            this.btn_CalAutoExp.Text = "计算自动曝光";
            this.btn_CalAutoExp.UseVisualStyleBackColor = true;
            this.btn_CalAutoExp.Click += new System.EventHandler(this.btn_CalAutoExp_Click);
            // 
            // cb_DistortCorrect
            // 
            this.cb_DistortCorrect.AutoSize = true;
            this.cb_DistortCorrect.Location = new System.Drawing.Point(59, 1250);
            this.cb_DistortCorrect.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.cb_DistortCorrect.Name = "cb_DistortCorrect";
            this.cb_DistortCorrect.Size = new System.Drawing.Size(120, 25);
            this.cb_DistortCorrect.TabIndex = 6;
            this.cb_DistortCorrect.Text = "畸变校正";
            this.cb_DistortCorrect.UseVisualStyleBackColor = true;
            // 
            // cb_UniformFieldCorrect
            // 
            this.cb_UniformFieldCorrect.AutoSize = true;
            this.cb_UniformFieldCorrect.Location = new System.Drawing.Point(59, 1214);
            this.cb_UniformFieldCorrect.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.cb_UniformFieldCorrect.Name = "cb_UniformFieldCorrect";
            this.cb_UniformFieldCorrect.Size = new System.Drawing.Size(141, 25);
            this.cb_UniformFieldCorrect.TabIndex = 6;
            this.cb_UniformFieldCorrect.Text = "均匀场校正";
            this.cb_UniformFieldCorrect.UseVisualStyleBackColor = true;
            // 
            // cb_BadPixelCorrect
            // 
            this.cb_BadPixelCorrect.AutoSize = true;
            this.cb_BadPixelCorrect.Location = new System.Drawing.Point(59, 1173);
            this.cb_BadPixelCorrect.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.cb_BadPixelCorrect.Name = "cb_BadPixelCorrect";
            this.cb_BadPixelCorrect.Size = new System.Drawing.Size(120, 25);
            this.cb_BadPixelCorrect.TabIndex = 6;
            this.cb_BadPixelCorrect.Text = "坏点校正";
            this.cb_BadPixelCorrect.UseVisualStyleBackColor = true;
            // 
            // cb_FourColorCorrect
            // 
            this.cb_FourColorCorrect.AutoSize = true;
            this.cb_FourColorCorrect.Location = new System.Drawing.Point(59, 1096);
            this.cb_FourColorCorrect.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.cb_FourColorCorrect.Name = "cb_FourColorCorrect";
            this.cb_FourColorCorrect.Size = new System.Drawing.Size(120, 25);
            this.cb_FourColorCorrect.TabIndex = 6;
            this.cb_FourColorCorrect.Text = "四色校正";
            this.cb_FourColorCorrect.UseVisualStyleBackColor = true;
            this.cb_FourColorCorrect.CheckedChanged += new System.EventHandler(this.cb_FourColorCorrect_CheckedChanged);
            // 
            // cb_DarkNoiseCorrect
            // 
            this.cb_DarkNoiseCorrect.AutoSize = true;
            this.cb_DarkNoiseCorrect.Location = new System.Drawing.Point(59, 983);
            this.cb_DarkNoiseCorrect.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.cb_DarkNoiseCorrect.Name = "cb_DarkNoiseCorrect";
            this.cb_DarkNoiseCorrect.Size = new System.Drawing.Size(141, 25);
            this.cb_DarkNoiseCorrect.TabIndex = 6;
            this.cb_DarkNoiseCorrect.Text = "暗噪声校正";
            this.cb_DarkNoiseCorrect.UseVisualStyleBackColor = true;
            // 
            // cb_AutoExp
            // 
            this.cb_AutoExp.AutoSize = true;
            this.cb_AutoExp.Location = new System.Drawing.Point(231, 983);
            this.cb_AutoExp.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.cb_AutoExp.Name = "cb_AutoExp";
            this.cb_AutoExp.Size = new System.Drawing.Size(120, 25);
            this.cb_AutoExp.TabIndex = 5;
            this.cb_AutoExp.Text = "自动曝光";
            this.cb_AutoExp.UseVisualStyleBackColor = true;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(242, 808);
            this.label5.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(32, 21);
            this.label5.TabIndex = 4;
            this.label5.Text = "ms";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(63, 657);
            this.label2.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(85, 21);
            this.label2.TabIndex = 4;
            this.label2.Text = "TIF名称";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(135, 1362);
            this.label4.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(85, 21);
            this.label4.TabIndex = 4;
            this.label4.Text = "TIF路径";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(59, 808);
            this.label3.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(52, 21);
            this.label3.TabIndex = 4;
            this.label3.Text = "时间";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(63, 703);
            this.label1.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(52, 21);
            this.label1.TabIndex = 4;
            this.label1.Text = "次数";
            // 
            // tb_TiffName
            // 
            this.tb_TiffName.Location = new System.Drawing.Point(158, 654);
            this.tb_TiffName.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.tb_TiffName.Name = "tb_TiffName";
            this.tb_TiffName.Size = new System.Drawing.Size(170, 31);
            this.tb_TiffName.TabIndex = 3;
            this.tb_TiffName.Text = "ceshi001";
            // 
            // tb_TiffPath
            // 
            this.tb_TiffPath.Location = new System.Drawing.Point(230, 1355);
            this.tb_TiffPath.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.tb_TiffPath.Name = "tb_TiffPath";
            this.tb_TiffPath.Size = new System.Drawing.Size(169, 31);
            this.tb_TiffPath.TabIndex = 3;
            this.tb_TiffPath.Text = "D:\\tiff";
            // 
            // tb_Exp
            // 
            this.tb_Exp.Location = new System.Drawing.Point(142, 797);
            this.tb_Exp.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.tb_Exp.Name = "tb_Exp";
            this.tb_Exp.Size = new System.Drawing.Size(88, 31);
            this.tb_Exp.TabIndex = 3;
            this.tb_Exp.Text = "100";
            // 
            // tb_Count
            // 
            this.tb_Count.Location = new System.Drawing.Point(158, 696);
            this.tb_Count.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.tb_Count.Name = "tb_Count";
            this.tb_Count.Size = new System.Drawing.Size(170, 31);
            this.tb_Count.TabIndex = 3;
            this.tb_Count.Text = "1";
            // 
            // btn_ConfigFile
            // 
            this.btn_ConfigFile.Location = new System.Drawing.Point(226, 1305);
            this.btn_ConfigFile.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.btn_ConfigFile.Name = "btn_ConfigFile";
            this.btn_ConfigFile.Size = new System.Drawing.Size(170, 39);
            this.btn_ConfigFile.TabIndex = 2;
            this.btn_ConfigFile.Text = "配置文件";
            this.btn_ConfigFile.UseVisualStyleBackColor = true;
            this.btn_ConfigFile.Click += new System.EventHandler(this.btn_ConfigFile_Click);
            // 
            // btn_close
            // 
            this.btn_close.Enabled = false;
            this.btn_close.Location = new System.Drawing.Point(110, 358);
            this.btn_close.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.btn_close.Name = "btn_close";
            this.btn_close.Size = new System.Drawing.Size(170, 39);
            this.btn_close.TabIndex = 0;
            this.btn_close.Text = "关闭";
            this.btn_close.UseVisualStyleBackColor = true;
            this.btn_close.Click += new System.EventHandler(this.btn_StopLive_Click);
            // 
            // btn_Meas
            // 
            this.btn_Meas.Location = new System.Drawing.Point(110, 403);
            this.btn_Meas.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.btn_Meas.Name = "btn_Meas";
            this.btn_Meas.Size = new System.Drawing.Size(170, 39);
            this.btn_Meas.TabIndex = 1;
            this.btn_Meas.Text = "测量";
            this.btn_Meas.UseVisualStyleBackColor = true;
            this.btn_Meas.Click += new System.EventHandler(this.btn_Meas_Click);
            // 
            // timer1
            // 
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // menuStrip1
            // 
            this.menuStrip1.GripMargin = new System.Windows.Forms.Padding(2, 2, 0, 2);
            this.menuStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.显示ToolStripMenuItem,
            this.基本参数设置ToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Padding = new System.Windows.Forms.Padding(7, 3, 0, 3);
            this.menuStrip1.Size = new System.Drawing.Size(2149, 38);
            this.menuStrip1.TabIndex = 4;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // 显示ToolStripMenuItem
            // 
            this.显示ToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.MenuItem_ShowCenterLine});
            this.显示ToolStripMenuItem.Name = "显示ToolStripMenuItem";
            this.显示ToolStripMenuItem.Size = new System.Drawing.Size(114, 32);
            this.显示ToolStripMenuItem.Text = "显示设置";
            // 
            // MenuItem_ShowCenterLine
            // 
            this.MenuItem_ShowCenterLine.Name = "MenuItem_ShowCenterLine";
            this.MenuItem_ShowCenterLine.Size = new System.Drawing.Size(234, 40);
            this.MenuItem_ShowCenterLine.Text = "显示中心线";
            this.MenuItem_ShowCenterLine.Click += new System.EventHandler(this.MenuItem_ShowCenterLine_Click);
            // 
            // 基本参数设置ToolStripMenuItem
            // 
            this.基本参数设置ToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.Camera_MenuItem,
            this.Channels_MenuItem,
            this.Calibration_MenuItem,
            this.ExpTime_MenuItem});
            this.基本参数设置ToolStripMenuItem.Name = "基本参数设置ToolStripMenuItem";
            this.基本参数设置ToolStripMenuItem.Size = new System.Drawing.Size(156, 32);
            this.基本参数设置ToolStripMenuItem.Text = "基本参数设置";
            // 
            // Camera_MenuItem
            // 
            this.Camera_MenuItem.Name = "Camera_MenuItem";
            this.Camera_MenuItem.Size = new System.Drawing.Size(297, 40);
            this.Camera_MenuItem.Text = "相机参数设置";
            this.Camera_MenuItem.Click += new System.EventHandler(this.Camera_MenuItem_Click);
            // 
            // Channels_MenuItem
            // 
            this.Channels_MenuItem.Name = "Channels_MenuItem";
            this.Channels_MenuItem.Size = new System.Drawing.Size(297, 40);
            this.Channels_MenuItem.Text = "通道参数设置";
            this.Channels_MenuItem.Click += new System.EventHandler(this.Channels_MenuItem_Click);
            // 
            // Calibration_MenuItem
            // 
            this.Calibration_MenuItem.Name = "Calibration_MenuItem";
            this.Calibration_MenuItem.Size = new System.Drawing.Size(297, 40);
            this.Calibration_MenuItem.Text = "校正参数设置";
            this.Calibration_MenuItem.Click += new System.EventHandler(this.Calibration_MenuItem_Click);
            // 
            // ExpTime_MenuItem
            // 
            this.ExpTime_MenuItem.Name = "ExpTime_MenuItem";
            this.ExpTime_MenuItem.Size = new System.Drawing.Size(297, 40);
            this.ExpTime_MenuItem.Text = "自动曝光参数设置";
            this.ExpTime_MenuItem.Click += new System.EventHandler(this.ExpTime_MenuItem_Click);
            // 
            // timer2
            // 
            this.timer2.Tick += new System.EventHandler(this.timer2_Tick);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(11F, 21F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(2149, 1453);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.menuStrip1);
            this.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "COLOR VISION";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.Form1_FormClosed);
            this.Load += new System.EventHandler(this.Form1_Load);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.Form1_MouseMove);
            this.Resize += new System.EventHandler(this.Form1_Resize);
            this.panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btn_Connect;
        private System.Windows.Forms.Button btn_MeasTif;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tb_Count;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox tb_TiffName;
        private System.Windows.Forms.CheckBox cb_AutoExp;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox tb_Exp;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox tb_TiffPath;
        private System.Windows.Forms.CheckBox cb_DarkNoiseCorrect;
        private System.Windows.Forms.CheckBox cb_FourColorCorrect;
        private System.Windows.Forms.Button btn_ConfigFile;
        private System.Windows.Forms.CheckBox cb_UniformFieldCorrect;
        private System.Windows.Forms.CheckBox cb_BadPixelCorrect;
        private System.Windows.Forms.Button btn_CalAutoExp;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.Button btn_close;
        private System.Windows.Forms.Button btn_SetExp;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Button btn_Meas;
        private System.Windows.Forms.CheckBox cb_DistortCorrect;
        private System.Windows.Forms.TextBox tb_e;
        private System.Windows.Forms.TextBox tb_s;
        private System.Windows.Forms.TextBox tb_p;
        private System.Windows.Forms.Button btn_SetGain;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox tb_Gain;
        private System.Windows.Forms.CheckBox cb_MulColorCorrect;
        private System.Windows.Forms.Button btn_Test;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.ComboBox cb_CM_TYPE;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.ComboBox cb_Channels;
        private System.Windows.Forms.CheckBox cb_MonoCorrect;
        private System.Windows.Forms.CheckBox cb_LumCorrect;
        private System.Windows.Forms.ComboBox cb_get_mode;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.ComboBox cb_bpp;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.ComboBox cb_CM_ID;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem 显示ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem MenuItem_ShowCenterLine;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.Button btn_Distortion;
        private System.Windows.Forms.Button btn_SFR;
        private System.Windows.Forms.Button btn_FOV;
        private System.Windows.Forms.CheckBox checkBox1;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.TextBox tb_Exp3;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.TextBox tb_Exp2;
        private System.Windows.Forms.ToolStripMenuItem 基本参数设置ToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem Camera_MenuItem;
        private System.Windows.Forms.ToolStripMenuItem Channels_MenuItem;
        private System.Windows.Forms.ToolStripMenuItem Calibration_MenuItem;
        private System.Windows.Forms.ToolStripMenuItem ExpTime_MenuItem;
        private System.Windows.Forms.Timer timer2;
        private System.Windows.Forms.CheckBox cb_DSNU;
        private System.Windows.Forms.Button btn_reset;
        private System.Windows.Forms.ComboBox cb_CM_MODE;
        private System.Windows.Forms.Label label15;
    }
}

