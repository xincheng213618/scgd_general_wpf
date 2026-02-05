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
            components = new System.ComponentModel.Container();
            btn_Connect = new System.Windows.Forms.Button();
            btn_MeasTif = new System.Windows.Forms.Button();
            panel1 = new System.Windows.Forms.Panel();
            pictureBox1 = new System.Windows.Forms.PictureBox();
            panel2 = new System.Windows.Forms.Panel();
            cb_CM_MODE = new System.Windows.Forms.ComboBox();
            label15 = new System.Windows.Forms.Label();
            btn_reset = new System.Windows.Forms.Button();
            cb_DSNU = new System.Windows.Forms.CheckBox();
            label16 = new System.Windows.Forms.Label();
            tb_Exp3 = new System.Windows.Forms.TextBox();
            label14 = new System.Windows.Forms.Label();
            tb_Exp2 = new System.Windows.Forms.TextBox();
            button2 = new System.Windows.Forms.Button();
            checkBox1 = new System.Windows.Forms.CheckBox();
            btn_Distortion = new System.Windows.Forms.Button();
            btn_SFR = new System.Windows.Forms.Button();
            btn_FOV = new System.Windows.Forms.Button();
            label13 = new System.Windows.Forms.Label();
            button1 = new System.Windows.Forms.Button();
            textBox1 = new System.Windows.Forms.TextBox();
            cb_CM_ID = new System.Windows.Forms.ComboBox();
            label12 = new System.Windows.Forms.Label();
            cb_bpp = new System.Windows.Forms.ComboBox();
            label11 = new System.Windows.Forms.Label();
            cb_get_mode = new System.Windows.Forms.ComboBox();
            label10 = new System.Windows.Forms.Label();
            cb_LumCorrect = new System.Windows.Forms.CheckBox();
            cb_MonoCorrect = new System.Windows.Forms.CheckBox();
            cb_Channels = new System.Windows.Forms.ComboBox();
            label9 = new System.Windows.Forms.Label();
            cb_CM_TYPE = new System.Windows.Forms.ComboBox();
            label8 = new System.Windows.Forms.Label();
            btn_Test = new System.Windows.Forms.Button();
            cb_MulColorCorrect = new System.Windows.Forms.CheckBox();
            btn_SetGain = new System.Windows.Forms.Button();
            label6 = new System.Windows.Forms.Label();
            label7 = new System.Windows.Forms.Label();
            tb_Gain = new System.Windows.Forms.TextBox();
            tb_p = new System.Windows.Forms.TextBox();
            tb_e = new System.Windows.Forms.TextBox();
            tb_s = new System.Windows.Forms.TextBox();
            btn_SetExp = new System.Windows.Forms.Button();
            btn_CalAutoExp = new System.Windows.Forms.Button();
            cb_DistortCorrect = new System.Windows.Forms.CheckBox();
            cb_UniformFieldCorrect = new System.Windows.Forms.CheckBox();
            cb_BadPixelCorrect = new System.Windows.Forms.CheckBox();
            cb_FourColorCorrect = new System.Windows.Forms.CheckBox();
            cb_DarkNoiseCorrect = new System.Windows.Forms.CheckBox();
            cb_AutoExp = new System.Windows.Forms.CheckBox();
            label5 = new System.Windows.Forms.Label();
            label2 = new System.Windows.Forms.Label();
            label4 = new System.Windows.Forms.Label();
            label3 = new System.Windows.Forms.Label();
            label1 = new System.Windows.Forms.Label();
            tb_TiffName = new System.Windows.Forms.TextBox();
            tb_TiffPath = new System.Windows.Forms.TextBox();
            tb_Exp = new System.Windows.Forms.TextBox();
            tb_Count = new System.Windows.Forms.TextBox();
            btn_ConfigFile = new System.Windows.Forms.Button();
            btn_close = new System.Windows.Forms.Button();
            btn_Meas = new System.Windows.Forms.Button();
            timer1 = new System.Windows.Forms.Timer(components);
            menuStrip1 = new System.Windows.Forms.MenuStrip();
            显示ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            MenuItem_ShowCenterLine = new System.Windows.Forms.ToolStripMenuItem();
            基本参数设置ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            Camera_MenuItem = new System.Windows.Forms.ToolStripMenuItem();
            Channels_MenuItem = new System.Windows.Forms.ToolStripMenuItem();
            Calibration_MenuItem = new System.Windows.Forms.ToolStripMenuItem();
            ExpTime_MenuItem = new System.Windows.Forms.ToolStripMenuItem();
            timer2 = new System.Windows.Forms.Timer(components);
            panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            panel2.SuspendLayout();
            menuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // btn_Connect
            // 
            btn_Connect.Location = new System.Drawing.Point(110, 267);
            btn_Connect.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            btn_Connect.Name = "btn_Connect";
            btn_Connect.Size = new System.Drawing.Size(170, 39);
            btn_Connect.TabIndex = 0;
            btn_Connect.Text = "连接";
            btn_Connect.UseVisualStyleBackColor = true;
            btn_Connect.Click += btn_MeasConnect_Click;
            // 
            // btn_MeasTif
            // 
            btn_MeasTif.Location = new System.Drawing.Point(110, 449);
            btn_MeasTif.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            btn_MeasTif.Name = "btn_MeasTif";
            btn_MeasTif.Size = new System.Drawing.Size(170, 39);
            btn_MeasTif.TabIndex = 1;
            btn_MeasTif.Text = "测量tif";
            btn_MeasTif.UseVisualStyleBackColor = true;
            btn_MeasTif.Click += btn_MeasTif_Click;
            // 
            // panel1
            // 
            panel1.AutoScroll = true;
            panel1.Controls.Add(pictureBox1);
            panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            panel1.Location = new System.Drawing.Point(448, 38);
            panel1.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            panel1.Name = "panel1";
            panel1.Size = new System.Drawing.Size(1701, 1415);
            panel1.TabIndex = 2;
            // 
            // pictureBox1
            // 
            pictureBox1.BackColor = System.Drawing.SystemColors.ActiveBorder;
            pictureBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            pictureBox1.Location = new System.Drawing.Point(0, 0);
            pictureBox1.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new System.Drawing.Size(1701, 1415);
            pictureBox1.TabIndex = 0;
            pictureBox1.TabStop = false;
            pictureBox1.Paint += pictureBox1_Paint;
            pictureBox1.MouseDown += pictureBox1_MouseDown;
            pictureBox1.MouseEnter += pictureBox1_MouseEnter;
            pictureBox1.MouseLeave += pictureBox1_MouseLeave;
            pictureBox1.MouseMove += pictureBox1_MouseMove;
            pictureBox1.MouseUp += pictureBox1_MouseUp;
            pictureBox1.MouseWheel += pictureBox1_MouseWheel;
            // 
            // panel2
            // 
            panel2.Controls.Add(cb_CM_MODE);
            panel2.Controls.Add(label15);
            panel2.Controls.Add(btn_reset);
            panel2.Controls.Add(cb_DSNU);
            panel2.Controls.Add(label16);
            panel2.Controls.Add(tb_Exp3);
            panel2.Controls.Add(label14);
            panel2.Controls.Add(tb_Exp2);
            panel2.Controls.Add(button2);
            panel2.Controls.Add(checkBox1);
            panel2.Controls.Add(btn_Distortion);
            panel2.Controls.Add(btn_SFR);
            panel2.Controls.Add(btn_FOV);
            panel2.Controls.Add(label13);
            panel2.Controls.Add(button1);
            panel2.Controls.Add(textBox1);
            panel2.Controls.Add(cb_CM_ID);
            panel2.Controls.Add(label12);
            panel2.Controls.Add(cb_bpp);
            panel2.Controls.Add(label11);
            panel2.Controls.Add(cb_get_mode);
            panel2.Controls.Add(label10);
            panel2.Controls.Add(cb_LumCorrect);
            panel2.Controls.Add(cb_MonoCorrect);
            panel2.Controls.Add(cb_Channels);
            panel2.Controls.Add(label9);
            panel2.Controls.Add(cb_CM_TYPE);
            panel2.Controls.Add(label8);
            panel2.Controls.Add(btn_Test);
            panel2.Controls.Add(cb_MulColorCorrect);
            panel2.Controls.Add(btn_SetGain);
            panel2.Controls.Add(label6);
            panel2.Controls.Add(label7);
            panel2.Controls.Add(tb_Gain);
            panel2.Controls.Add(tb_p);
            panel2.Controls.Add(tb_e);
            panel2.Controls.Add(tb_s);
            panel2.Controls.Add(btn_SetExp);
            panel2.Controls.Add(btn_CalAutoExp);
            panel2.Controls.Add(cb_DistortCorrect);
            panel2.Controls.Add(cb_UniformFieldCorrect);
            panel2.Controls.Add(cb_BadPixelCorrect);
            panel2.Controls.Add(cb_FourColorCorrect);
            panel2.Controls.Add(cb_DarkNoiseCorrect);
            panel2.Controls.Add(cb_AutoExp);
            panel2.Controls.Add(label5);
            panel2.Controls.Add(label2);
            panel2.Controls.Add(label4);
            panel2.Controls.Add(label3);
            panel2.Controls.Add(label1);
            panel2.Controls.Add(tb_TiffName);
            panel2.Controls.Add(tb_TiffPath);
            panel2.Controls.Add(tb_Exp);
            panel2.Controls.Add(tb_Count);
            panel2.Controls.Add(btn_ConfigFile);
            panel2.Controls.Add(btn_close);
            panel2.Controls.Add(btn_Connect);
            panel2.Controls.Add(btn_Meas);
            panel2.Controls.Add(btn_MeasTif);
            panel2.Dock = System.Windows.Forms.DockStyle.Left;
            panel2.Location = new System.Drawing.Point(0, 38);
            panel2.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            panel2.Name = "panel2";
            panel2.Size = new System.Drawing.Size(448, 1415);
            panel2.TabIndex = 3;
            // 
            // cb_CM_MODE
            // 
            cb_CM_MODE.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cb_CM_MODE.FormattingEnabled = true;
            cb_CM_MODE.Items.AddRange(new object[] { "CV_MODE", "BV_MODE", "LV_MODE", "LVTOBV_MODE" });
            cb_CM_MODE.Location = new System.Drawing.Point(188, 53);
            cb_CM_MODE.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            cb_CM_MODE.Name = "cb_CM_MODE";
            cb_CM_MODE.Size = new System.Drawing.Size(168, 29);
            cb_CM_MODE.TabIndex = 169;
            cb_CM_MODE.SelectedIndexChanged += cb_CM_MODE_SelectedIndexChanged;
            // 
            // label15
            // 
            label15.AutoSize = true;
            label15.Location = new System.Drawing.Point(55, 63);
            label15.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            label15.Name = "label15";
            label15.Size = new System.Drawing.Size(115, 21);
            label15.TabIndex = 168;
            label15.Text = "相机模式：";
            // 
            // btn_reset
            // 
            btn_reset.Location = new System.Drawing.Point(110, 311);
            btn_reset.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            btn_reset.Name = "btn_reset";
            btn_reset.Size = new System.Drawing.Size(170, 39);
            btn_reset.TabIndex = 167;
            btn_reset.Text = "复位";
            btn_reset.UseVisualStyleBackColor = true;
            btn_reset.Click += btn_reset_Click;
            // 
            // cb_DSNU
            // 
            cb_DSNU.AutoSize = true;
            cb_DSNU.Location = new System.Drawing.Point(59, 942);
            cb_DSNU.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            cb_DSNU.Name = "cb_DSNU";
            cb_DSNU.Size = new System.Drawing.Size(80, 25);
            cb_DSNU.TabIndex = 166;
            cb_DSNU.Text = "DSNU";
            cb_DSNU.UseVisualStyleBackColor = true;
            // 
            // label16
            // 
            label16.AutoSize = true;
            label16.Location = new System.Drawing.Point(239, 909);
            label16.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            label16.Name = "label16";
            label16.Size = new System.Drawing.Size(32, 21);
            label16.TabIndex = 165;
            label16.Text = "ms";
            // 
            // tb_Exp3
            // 
            tb_Exp3.Location = new System.Drawing.Point(139, 895);
            tb_Exp3.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            tb_Exp3.Name = "tb_Exp3";
            tb_Exp3.Size = new System.Drawing.Size(88, 31);
            tb_Exp3.TabIndex = 164;
            tb_Exp3.Text = "100";
            // 
            // label14
            // 
            label14.AutoSize = true;
            label14.Location = new System.Drawing.Point(239, 853);
            label14.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            label14.Name = "label14";
            label14.Size = new System.Drawing.Size(32, 21);
            label14.TabIndex = 161;
            label14.Text = "ms";
            // 
            // tb_Exp2
            // 
            tb_Exp2.Location = new System.Drawing.Point(139, 846);
            tb_Exp2.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            tb_Exp2.Name = "tb_Exp2";
            tb_Exp2.Size = new System.Drawing.Size(88, 31);
            tb_Exp2.TabIndex = 160;
            tb_Exp2.Text = "100";
            // 
            // button2
            // 
            button2.Location = new System.Drawing.Point(242, 1256);
            button2.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            button2.Name = "button2";
            button2.Size = new System.Drawing.Size(138, 39);
            button2.TabIndex = 159;
            button2.Text = "显示设置";
            button2.UseVisualStyleBackColor = true;
            button2.Click += button2_Click;
            // 
            // checkBox1
            // 
            checkBox1.AutoSize = true;
            checkBox1.Location = new System.Drawing.Point(242, 1067);
            checkBox1.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            checkBox1.Name = "checkBox1";
            checkBox1.Size = new System.Drawing.Size(78, 25);
            checkBox1.TabIndex = 158;
            checkBox1.Text = "框选";
            checkBox1.UseVisualStyleBackColor = true;
            checkBox1.CheckedChanged += checkBox1_CheckedChanged;
            // 
            // btn_Distortion
            // 
            btn_Distortion.Location = new System.Drawing.Point(242, 1207);
            btn_Distortion.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            btn_Distortion.Name = "btn_Distortion";
            btn_Distortion.Size = new System.Drawing.Size(138, 39);
            btn_Distortion.TabIndex = 157;
            btn_Distortion.Text = "执行Disto";
            btn_Distortion.UseVisualStyleBackColor = true;
            btn_Distortion.Click += btn_Distortion_Click;
            // 
            // btn_SFR
            // 
            btn_SFR.Location = new System.Drawing.Point(242, 1152);
            btn_SFR.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            btn_SFR.Name = "btn_SFR";
            btn_SFR.Size = new System.Drawing.Size(138, 39);
            btn_SFR.TabIndex = 156;
            btn_SFR.Text = "执行 SFR";
            btn_SFR.UseVisualStyleBackColor = true;
            btn_SFR.Click += btn_SFR_Click;
            // 
            // btn_FOV
            // 
            btn_FOV.Location = new System.Drawing.Point(242, 1103);
            btn_FOV.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            btn_FOV.Name = "btn_FOV";
            btn_FOV.Size = new System.Drawing.Size(138, 39);
            btn_FOV.TabIndex = 154;
            btn_FOV.Text = "执行FOV";
            btn_FOV.UseVisualStyleBackColor = true;
            btn_FOV.Click += btn_FOV_Click;
            // 
            // label13
            // 
            label13.AutoSize = true;
            label13.Location = new System.Drawing.Point(292, 557);
            label13.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            label13.Name = "label13";
            label13.Size = new System.Drawing.Size(129, 21);
            label13.TabIndex = 30;
            label13.Text = "阈值(0-255)";
            // 
            // button1
            // 
            button1.Location = new System.Drawing.Point(47, 549);
            button1.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            button1.Name = "button1";
            button1.Size = new System.Drawing.Size(129, 39);
            button1.TabIndex = 29;
            button1.Text = "计算位置";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // textBox1
            // 
            textBox1.Location = new System.Drawing.Point(198, 552);
            textBox1.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            textBox1.Name = "textBox1";
            textBox1.Size = new System.Drawing.Size(70, 31);
            textBox1.TabIndex = 28;
            textBox1.Text = "30";
            // 
            // cb_CM_ID
            // 
            cb_CM_ID.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cb_CM_ID.FormattingEnabled = true;
            cb_CM_ID.Items.AddRange(new object[] { "CV_Q", "LV_Q", "BV_Q", "MIL_CL", "MIL_CXP", "BV_H", "LV_H", "HK_CXP" });
            cb_CM_ID.Location = new System.Drawing.Point(188, 94);
            cb_CM_ID.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            cb_CM_ID.Name = "cb_CM_ID";
            cb_CM_ID.Size = new System.Drawing.Size(168, 29);
            cb_CM_ID.TabIndex = 27;
            cb_CM_ID.SelectedIndexChanged += cb_CM_ID_SelectedIndexChanged;
            // 
            // label12
            // 
            label12.AutoSize = true;
            label12.Location = new System.Drawing.Point(55, 102);
            label12.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            label12.Name = "label12";
            label12.Size = new System.Drawing.Size(95, 21);
            label12.TabIndex = 26;
            label12.Text = "相机ID：";
            // 
            // cb_bpp
            // 
            cb_bpp.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cb_bpp.FormattingEnabled = true;
            cb_bpp.Items.AddRange(new object[] { "8", "16" });
            cb_bpp.Location = new System.Drawing.Point(188, 220);
            cb_bpp.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            cb_bpp.Name = "cb_bpp";
            cb_bpp.Size = new System.Drawing.Size(168, 29);
            cb_bpp.TabIndex = 25;
            cb_bpp.SelectedIndexChanged += cb_bpp_SelectedIndexChanged;
            // 
            // label11
            // 
            label11.AutoSize = true;
            label11.Location = new System.Drawing.Point(55, 228);
            label11.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            label11.Name = "label11";
            label11.Size = new System.Drawing.Size(115, 21);
            label11.TabIndex = 24;
            label11.Text = "像素位数：";
            // 
            // cb_get_mode
            // 
            cb_get_mode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cb_get_mode.FormattingEnabled = true;
            cb_get_mode.Items.AddRange(new object[] { "Measure_Normal", "Live ", "Measure_Fast", "Measure_FastEx " });
            cb_get_mode.Location = new System.Drawing.Point(188, 178);
            cb_get_mode.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            cb_get_mode.Name = "cb_get_mode";
            cb_get_mode.Size = new System.Drawing.Size(168, 29);
            cb_get_mode.TabIndex = 23;
            cb_get_mode.SelectedIndexChanged += cb_get_mode_SelectedIndexChanged;
            // 
            // label10
            // 
            label10.AutoSize = true;
            label10.Location = new System.Drawing.Point(55, 186);
            label10.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            label10.Name = "label10";
            label10.Size = new System.Drawing.Size(115, 21);
            label10.TabIndex = 22;
            label10.Text = "取图模式：";
            // 
            // cb_LumCorrect
            // 
            cb_LumCorrect.AutoSize = true;
            cb_LumCorrect.Location = new System.Drawing.Point(59, 1019);
            cb_LumCorrect.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            cb_LumCorrect.Name = "cb_LumCorrect";
            cb_LumCorrect.Size = new System.Drawing.Size(177, 25);
            cb_LumCorrect.TabIndex = 21;
            cb_LumCorrect.Text = "Luminance校正";
            cb_LumCorrect.UseVisualStyleBackColor = true;
            // 
            // cb_MonoCorrect
            // 
            cb_MonoCorrect.AutoSize = true;
            cb_MonoCorrect.Location = new System.Drawing.Point(59, 1060);
            cb_MonoCorrect.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            cb_MonoCorrect.Name = "cb_MonoCorrect";
            cb_MonoCorrect.Size = new System.Drawing.Size(120, 25);
            cb_MonoCorrect.TabIndex = 20;
            cb_MonoCorrect.Text = "单色校正";
            cb_MonoCorrect.UseVisualStyleBackColor = true;
            cb_MonoCorrect.CheckedChanged += cb_MonoCorrect_CheckedChanged;
            // 
            // cb_Channels
            // 
            cb_Channels.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cb_Channels.Enabled = false;
            cb_Channels.FormattingEnabled = true;
            cb_Channels.Items.AddRange(new object[] { "1", "3" });
            cb_Channels.Location = new System.Drawing.Point(188, 136);
            cb_Channels.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            cb_Channels.Name = "cb_Channels";
            cb_Channels.Size = new System.Drawing.Size(168, 29);
            cb_Channels.TabIndex = 19;
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Location = new System.Drawing.Point(55, 144);
            label9.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            label9.Name = "label9";
            label9.Size = new System.Drawing.Size(94, 21);
            label9.TabIndex = 18;
            label9.Text = "通道数：";
            // 
            // cb_CM_TYPE
            // 
            cb_CM_TYPE.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cb_CM_TYPE.FormattingEnabled = true;
            cb_CM_TYPE.Items.AddRange(new object[] { "QHY_USB", "HK_USB", "HK_CARD", "MIL_CL_CARD", "MIL_CXP_CARD", "NN_USB", "TOUP_USB", "HK_FG_CARD", "IKAP_CARD" });
            cb_CM_TYPE.Location = new System.Drawing.Point(188, 17);
            cb_CM_TYPE.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            cb_CM_TYPE.Name = "cb_CM_TYPE";
            cb_CM_TYPE.Size = new System.Drawing.Size(168, 29);
            cb_CM_TYPE.TabIndex = 17;
            cb_CM_TYPE.SelectedIndexChanged += cb_CM_TYPE_SelectedIndexChanged;
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Location = new System.Drawing.Point(55, 25);
            label8.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            label8.Name = "label8";
            label8.Size = new System.Drawing.Size(115, 21);
            label8.TabIndex = 16;
            label8.Text = "相机型号：";
            // 
            // btn_Test
            // 
            btn_Test.Location = new System.Drawing.Point(41, 1299);
            btn_Test.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            btn_Test.Name = "btn_Test";
            btn_Test.Size = new System.Drawing.Size(170, 39);
            btn_Test.TabIndex = 15;
            btn_Test.Text = "自动对焦测试";
            btn_Test.UseVisualStyleBackColor = true;
            btn_Test.Click += btn_Test_Click;
            // 
            // cb_MulColorCorrect
            // 
            cb_MulColorCorrect.AutoSize = true;
            cb_MulColorCorrect.Location = new System.Drawing.Point(59, 1137);
            cb_MulColorCorrect.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            cb_MulColorCorrect.Name = "cb_MulColorCorrect";
            cb_MulColorCorrect.Size = new System.Drawing.Size(120, 25);
            cb_MulColorCorrect.TabIndex = 14;
            cb_MulColorCorrect.Text = "多色校正";
            cb_MulColorCorrect.UseVisualStyleBackColor = true;
            cb_MulColorCorrect.CheckedChanged += cb_MulColorCorrect_CheckedChanged;
            // 
            // btn_SetGain
            // 
            btn_SetGain.Location = new System.Drawing.Point(279, 745);
            btn_SetGain.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            btn_SetGain.Name = "btn_SetGain";
            btn_SetGain.Size = new System.Drawing.Size(73, 39);
            btn_SetGain.TabIndex = 13;
            btn_SetGain.Text = "设置";
            btn_SetGain.UseVisualStyleBackColor = true;
            btn_SetGain.Click += btn_SetGain_Click;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new System.Drawing.Point(242, 755);
            label6.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            label6.Name = "label6";
            label6.Size = new System.Drawing.Size(32, 21);
            label6.TabIndex = 11;
            label6.Text = "dB";
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new System.Drawing.Point(55, 755);
            label7.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            label7.Name = "label7";
            label7.Size = new System.Drawing.Size(52, 21);
            label7.TabIndex = 12;
            label7.Text = "增益";
            // 
            // tb_Gain
            // 
            tb_Gain.Location = new System.Drawing.Point(139, 748);
            tb_Gain.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            tb_Gain.Name = "tb_Gain";
            tb_Gain.Size = new System.Drawing.Size(88, 31);
            tb_Gain.TabIndex = 10;
            tb_Gain.Text = "10";
            // 
            // tb_p
            // 
            tb_p.Location = new System.Drawing.Point(260, 601);
            tb_p.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            tb_p.Name = "tb_p";
            tb_p.Size = new System.Drawing.Size(70, 31);
            tb_p.TabIndex = 9;
            tb_p.Text = "0";
            // 
            // tb_e
            // 
            tb_e.Location = new System.Drawing.Point(169, 601);
            tb_e.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            tb_e.Name = "tb_e";
            tb_e.Size = new System.Drawing.Size(70, 31);
            tb_e.TabIndex = 9;
            tb_e.Text = "3";
            // 
            // tb_s
            // 
            tb_s.Location = new System.Drawing.Point(78, 601);
            tb_s.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            tb_s.Name = "tb_s";
            tb_s.Size = new System.Drawing.Size(70, 31);
            tb_s.TabIndex = 9;
            tb_s.Text = "1";
            // 
            // btn_SetExp
            // 
            btn_SetExp.Location = new System.Drawing.Point(280, 797);
            btn_SetExp.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            btn_SetExp.Name = "btn_SetExp";
            btn_SetExp.Size = new System.Drawing.Size(73, 39);
            btn_SetExp.TabIndex = 8;
            btn_SetExp.Text = "设置";
            btn_SetExp.UseVisualStyleBackColor = true;
            btn_SetExp.Click += btn_SetExp_Click;
            // 
            // btn_CalAutoExp
            // 
            btn_CalAutoExp.Location = new System.Drawing.Point(110, 493);
            btn_CalAutoExp.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            btn_CalAutoExp.Name = "btn_CalAutoExp";
            btn_CalAutoExp.Size = new System.Drawing.Size(170, 39);
            btn_CalAutoExp.TabIndex = 7;
            btn_CalAutoExp.Text = "计算自动曝光";
            btn_CalAutoExp.UseVisualStyleBackColor = true;
            btn_CalAutoExp.Click += btn_CalAutoExp_Click;
            // 
            // cb_DistortCorrect
            // 
            cb_DistortCorrect.AutoSize = true;
            cb_DistortCorrect.Location = new System.Drawing.Point(59, 1250);
            cb_DistortCorrect.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            cb_DistortCorrect.Name = "cb_DistortCorrect";
            cb_DistortCorrect.Size = new System.Drawing.Size(120, 25);
            cb_DistortCorrect.TabIndex = 6;
            cb_DistortCorrect.Text = "畸变校正";
            cb_DistortCorrect.UseVisualStyleBackColor = true;
            // 
            // cb_UniformFieldCorrect
            // 
            cb_UniformFieldCorrect.AutoSize = true;
            cb_UniformFieldCorrect.Location = new System.Drawing.Point(59, 1214);
            cb_UniformFieldCorrect.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            cb_UniformFieldCorrect.Name = "cb_UniformFieldCorrect";
            cb_UniformFieldCorrect.Size = new System.Drawing.Size(141, 25);
            cb_UniformFieldCorrect.TabIndex = 6;
            cb_UniformFieldCorrect.Text = "均匀场校正";
            cb_UniformFieldCorrect.UseVisualStyleBackColor = true;
            // 
            // cb_BadPixelCorrect
            // 
            cb_BadPixelCorrect.AutoSize = true;
            cb_BadPixelCorrect.Location = new System.Drawing.Point(59, 1173);
            cb_BadPixelCorrect.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            cb_BadPixelCorrect.Name = "cb_BadPixelCorrect";
            cb_BadPixelCorrect.Size = new System.Drawing.Size(120, 25);
            cb_BadPixelCorrect.TabIndex = 6;
            cb_BadPixelCorrect.Text = "坏点校正";
            cb_BadPixelCorrect.UseVisualStyleBackColor = true;
            // 
            // cb_FourColorCorrect
            // 
            cb_FourColorCorrect.AutoSize = true;
            cb_FourColorCorrect.Location = new System.Drawing.Point(59, 1096);
            cb_FourColorCorrect.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            cb_FourColorCorrect.Name = "cb_FourColorCorrect";
            cb_FourColorCorrect.Size = new System.Drawing.Size(120, 25);
            cb_FourColorCorrect.TabIndex = 6;
            cb_FourColorCorrect.Text = "四色校正";
            cb_FourColorCorrect.UseVisualStyleBackColor = true;
            cb_FourColorCorrect.CheckedChanged += cb_FourColorCorrect_CheckedChanged;
            // 
            // cb_DarkNoiseCorrect
            // 
            cb_DarkNoiseCorrect.AutoSize = true;
            cb_DarkNoiseCorrect.Location = new System.Drawing.Point(59, 983);
            cb_DarkNoiseCorrect.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            cb_DarkNoiseCorrect.Name = "cb_DarkNoiseCorrect";
            cb_DarkNoiseCorrect.Size = new System.Drawing.Size(141, 25);
            cb_DarkNoiseCorrect.TabIndex = 6;
            cb_DarkNoiseCorrect.Text = "暗噪声校正";
            cb_DarkNoiseCorrect.UseVisualStyleBackColor = true;
            // 
            // cb_AutoExp
            // 
            cb_AutoExp.AutoSize = true;
            cb_AutoExp.Location = new System.Drawing.Point(231, 983);
            cb_AutoExp.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            cb_AutoExp.Name = "cb_AutoExp";
            cb_AutoExp.Size = new System.Drawing.Size(120, 25);
            cb_AutoExp.TabIndex = 5;
            cb_AutoExp.Text = "自动曝光";
            cb_AutoExp.UseVisualStyleBackColor = true;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new System.Drawing.Point(242, 808);
            label5.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            label5.Name = "label5";
            label5.Size = new System.Drawing.Size(32, 21);
            label5.TabIndex = 4;
            label5.Text = "ms";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new System.Drawing.Point(63, 657);
            label2.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            label2.Name = "label2";
            label2.Size = new System.Drawing.Size(85, 21);
            label2.TabIndex = 4;
            label2.Text = "TIF名称";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new System.Drawing.Point(135, 1362);
            label4.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            label4.Name = "label4";
            label4.Size = new System.Drawing.Size(85, 21);
            label4.TabIndex = 4;
            label4.Text = "TIF路径";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new System.Drawing.Point(59, 808);
            label3.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            label3.Name = "label3";
            label3.Size = new System.Drawing.Size(52, 21);
            label3.TabIndex = 4;
            label3.Text = "时间";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new System.Drawing.Point(63, 703);
            label1.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            label1.Name = "label1";
            label1.Size = new System.Drawing.Size(52, 21);
            label1.TabIndex = 4;
            label1.Text = "次数";
            // 
            // tb_TiffName
            // 
            tb_TiffName.Location = new System.Drawing.Point(158, 654);
            tb_TiffName.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            tb_TiffName.Name = "tb_TiffName";
            tb_TiffName.Size = new System.Drawing.Size(170, 31);
            tb_TiffName.TabIndex = 3;
            tb_TiffName.Text = "ceshi001";
            // 
            // tb_TiffPath
            // 
            tb_TiffPath.Location = new System.Drawing.Point(230, 1355);
            tb_TiffPath.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            tb_TiffPath.Name = "tb_TiffPath";
            tb_TiffPath.Size = new System.Drawing.Size(169, 31);
            tb_TiffPath.TabIndex = 3;
            tb_TiffPath.Text = "D:\\tiff";
            // 
            // tb_Exp
            // 
            tb_Exp.Location = new System.Drawing.Point(142, 797);
            tb_Exp.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            tb_Exp.Name = "tb_Exp";
            tb_Exp.Size = new System.Drawing.Size(88, 31);
            tb_Exp.TabIndex = 3;
            tb_Exp.Text = "100";
            // 
            // tb_Count
            // 
            tb_Count.Location = new System.Drawing.Point(158, 696);
            tb_Count.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            tb_Count.Name = "tb_Count";
            tb_Count.Size = new System.Drawing.Size(170, 31);
            tb_Count.TabIndex = 3;
            tb_Count.Text = "1";
            // 
            // btn_ConfigFile
            // 
            btn_ConfigFile.Location = new System.Drawing.Point(226, 1305);
            btn_ConfigFile.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            btn_ConfigFile.Name = "btn_ConfigFile";
            btn_ConfigFile.Size = new System.Drawing.Size(170, 39);
            btn_ConfigFile.TabIndex = 2;
            btn_ConfigFile.Text = "配置文件";
            btn_ConfigFile.UseVisualStyleBackColor = true;
            btn_ConfigFile.Click += btn_ConfigFile_Click;
            // 
            // btn_close
            // 
            btn_close.Enabled = false;
            btn_close.Location = new System.Drawing.Point(110, 358);
            btn_close.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            btn_close.Name = "btn_close";
            btn_close.Size = new System.Drawing.Size(170, 39);
            btn_close.TabIndex = 0;
            btn_close.Text = "关闭";
            btn_close.UseVisualStyleBackColor = true;
            btn_close.Click += btn_StopLive_Click;
            // 
            // btn_Meas
            // 
            btn_Meas.Location = new System.Drawing.Point(110, 403);
            btn_Meas.Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            btn_Meas.Name = "btn_Meas";
            btn_Meas.Size = new System.Drawing.Size(170, 39);
            btn_Meas.TabIndex = 1;
            btn_Meas.Text = "测量";
            btn_Meas.UseVisualStyleBackColor = true;
            btn_Meas.Click += btn_Meas_Click;
            // 
            // timer1
            // 
            timer1.Tick += timer1_Tick;
            // 
            // menuStrip1
            // 
            menuStrip1.GripMargin = new System.Windows.Forms.Padding(2, 2, 0, 2);
            menuStrip1.ImageScalingSize = new System.Drawing.Size(24, 24);
            menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { 显示ToolStripMenuItem, 基本参数设置ToolStripMenuItem });
            menuStrip1.Location = new System.Drawing.Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Padding = new System.Windows.Forms.Padding(7, 3, 0, 3);
            menuStrip1.Size = new System.Drawing.Size(2149, 38);
            menuStrip1.TabIndex = 4;
            menuStrip1.Text = "menuStrip1";
            // 
            // 显示ToolStripMenuItem
            // 
            显示ToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { MenuItem_ShowCenterLine });
            显示ToolStripMenuItem.Name = "显示ToolStripMenuItem";
            显示ToolStripMenuItem.Size = new System.Drawing.Size(114, 32);
            显示ToolStripMenuItem.Text = "显示设置";
            // 
            // MenuItem_ShowCenterLine
            // 
            MenuItem_ShowCenterLine.Name = "MenuItem_ShowCenterLine";
            MenuItem_ShowCenterLine.Size = new System.Drawing.Size(234, 40);
            MenuItem_ShowCenterLine.Text = "显示中心线";
            MenuItem_ShowCenterLine.Click += MenuItem_ShowCenterLine_Click;
            // 
            // 基本参数设置ToolStripMenuItem
            // 
            基本参数设置ToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] { Camera_MenuItem, Channels_MenuItem, Calibration_MenuItem, ExpTime_MenuItem });
            基本参数设置ToolStripMenuItem.Name = "基本参数设置ToolStripMenuItem";
            基本参数设置ToolStripMenuItem.Size = new System.Drawing.Size(156, 32);
            基本参数设置ToolStripMenuItem.Text = "基本参数设置";
            // 
            // Camera_MenuItem
            // 
            Camera_MenuItem.Name = "Camera_MenuItem";
            Camera_MenuItem.Size = new System.Drawing.Size(297, 40);
            Camera_MenuItem.Text = "相机参数设置";
            Camera_MenuItem.Click += Camera_MenuItem_Click;
            // 
            // Channels_MenuItem
            // 
            Channels_MenuItem.Name = "Channels_MenuItem";
            Channels_MenuItem.Size = new System.Drawing.Size(297, 40);
            Channels_MenuItem.Text = "通道参数设置";
            Channels_MenuItem.Click += Channels_MenuItem_Click;
            // 
            // Calibration_MenuItem
            // 
            Calibration_MenuItem.Name = "Calibration_MenuItem";
            Calibration_MenuItem.Size = new System.Drawing.Size(297, 40);
            Calibration_MenuItem.Text = "校正参数设置";
            Calibration_MenuItem.Click += Calibration_MenuItem_Click;
            // 
            // ExpTime_MenuItem
            // 
            ExpTime_MenuItem.Name = "ExpTime_MenuItem";
            ExpTime_MenuItem.Size = new System.Drawing.Size(297, 40);
            ExpTime_MenuItem.Text = "自动曝光参数设置";
            ExpTime_MenuItem.Click += ExpTime_MenuItem_Click;
            // 
            // timer2
            // 
            timer2.Tick += timer2_Tick;
            // 
            // Form1
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(11F, 21F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(2149, 1453);
            Controls.Add(panel1);
            Controls.Add(panel2);
            Controls.Add(menuStrip1);
            Margin = new System.Windows.Forms.Padding(6, 4, 6, 4);
            Name = "Form1";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "COLOR VISION";
            FormClosing += Form1_FormClosing;
            FormClosed += Form1_FormClosed;
            Load += Form1_Load;
            MouseMove += Form1_MouseMove;
            Resize += Form1_Resize;
            panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            panel2.ResumeLayout(false);
            panel2.PerformLayout();
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();

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

