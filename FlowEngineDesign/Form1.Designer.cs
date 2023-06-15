namespace FlowEngineDesign
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
            this.stNodeTreeView1 = new ST.Library.UI.NodeEditor.STNodeTreeView();
            this.stNodeEditor1 = new ST.Library.UI.NodeEditor.STNodeEditor();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.splitContainer3 = new System.Windows.Forms.SplitContainer();
            this.stNodePropertyGrid1 = new ST.Library.UI.NodeEditor.STNodePropertyGrid();
            this.txt_log = new System.Windows.Forms.TextBox();
            this.panel1 = new System.Windows.Forms.Panel();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.checkBox1 = new System.Windows.Forms.CheckBox();
            this.btn_pause = new System.Windows.Forms.Button();
            this.btn_new = new System.Windows.Forms.Button();
            this.btn_stop = new System.Windows.Forms.Button();
            this.btn_start = new System.Windows.Forms.Button();
            this.btn_id_gen = new System.Windows.Forms.Button();
            this.btn_unsubscribe = new System.Windows.Forms.Button();
            this.tb_sn = new System.Windows.Forms.TextBox();
            this.txt主题 = new System.Windows.Forms.TextBox();
            this.btn_subscribe = new System.Windows.Forms.Button();
            this.btn_load = new System.Windows.Forms.Button();
            this.button_save_as = new System.Windows.Forms.Button();
            this.btn_save = new System.Windows.Forms.Button();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.delToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).BeginInit();
            this.splitContainer3.Panel1.SuspendLayout();
            this.splitContainer3.Panel2.SuspendLayout();
            this.splitContainer3.SuspendLayout();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // stNodeTreeView1
            // 
            this.stNodeTreeView1.AllowDrop = true;
            this.stNodeTreeView1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(35)))), ((int)(((byte)(35)))), ((int)(((byte)(35)))));
            this.stNodeTreeView1.Dock = System.Windows.Forms.DockStyle.Top;
            this.stNodeTreeView1.FolderCountColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(255)))));
            this.stNodeTreeView1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(220)))), ((int)(((byte)(220)))));
            this.stNodeTreeView1.ItemBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(45)))));
            this.stNodeTreeView1.ItemHoverColor = System.Drawing.Color.FromArgb(((int)(((byte)(50)))), ((int)(((byte)(125)))), ((int)(((byte)(125)))), ((int)(((byte)(125)))));
            this.stNodeTreeView1.Location = new System.Drawing.Point(0, 0);
            this.stNodeTreeView1.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.stNodeTreeView1.MinimumSize = new System.Drawing.Size(150, 90);
            this.stNodeTreeView1.Name = "stNodeTreeView1";
            this.stNodeTreeView1.ShowFolderCount = true;
            this.stNodeTreeView1.Size = new System.Drawing.Size(252, 537);
            this.stNodeTreeView1.TabIndex = 0;
            this.stNodeTreeView1.Text = "stNodeTreeView1";
            this.stNodeTreeView1.TextBoxColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.stNodeTreeView1.TitleColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            // 
            // stNodeEditor1
            // 
            this.stNodeEditor1.AllowDrop = true;
            this.stNodeEditor1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(34)))), ((int)(((byte)(34)))), ((int)(((byte)(34)))));
            this.stNodeEditor1.Curvature = 0.3F;
            this.stNodeEditor1.Location = new System.Drawing.Point(0, 205);
            this.stNodeEditor1.LocationBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(120)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.stNodeEditor1.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.stNodeEditor1.MarkBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(180)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.stNodeEditor1.MarkForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(180)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.stNodeEditor1.MinimumSize = new System.Drawing.Size(150, 150);
            this.stNodeEditor1.Name = "stNodeEditor1";
            this.stNodeEditor1.Size = new System.Drawing.Size(6000, 5193);
            this.stNodeEditor1.TabIndex = 1;
            this.stNodeEditor1.Text = "stNodeEditor1";
            this.stNodeEditor1.NodeAdded += new ST.Library.UI.NodeEditor.STNodeEditorEventHandler(this.stNodeEditor1_NodeAdded_1);
            this.stNodeEditor1.NodeRemoved += new ST.Library.UI.NodeEditor.STNodeEditorEventHandler(this.stNodeEditor1_NodeRemoved);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.splitContainer3);
            this.splitContainer1.Panel1.Controls.Add(this.stNodeTreeView1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.panel1);
            this.splitContainer1.Size = new System.Drawing.Size(1131, 841);
            this.splitContainer1.SplitterDistance = 252;
            this.splitContainer1.SplitterWidth = 6;
            this.splitContainer1.TabIndex = 2;
            // 
            // splitContainer3
            // 
            this.splitContainer3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer3.Location = new System.Drawing.Point(0, 537);
            this.splitContainer3.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.splitContainer3.Name = "splitContainer3";
            this.splitContainer3.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer3.Panel1
            // 
            this.splitContainer3.Panel1.Controls.Add(this.stNodePropertyGrid1);
            // 
            // splitContainer3.Panel2
            // 
            this.splitContainer3.Panel2.Controls.Add(this.txt_log);
            this.splitContainer3.Size = new System.Drawing.Size(252, 304);
            this.splitContainer3.SplitterDistance = 139;
            this.splitContainer3.SplitterWidth = 6;
            this.splitContainer3.TabIndex = 2;
            // 
            // stNodePropertyGrid1
            // 
            this.stNodePropertyGrid1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(35)))), ((int)(((byte)(35)))), ((int)(((byte)(35)))));
            this.stNodePropertyGrid1.DescriptionColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(184)))), ((int)(((byte)(134)))), ((int)(((byte)(11)))));
            this.stNodePropertyGrid1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.stNodePropertyGrid1.ErrorColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(165)))), ((int)(((byte)(42)))), ((int)(((byte)(42)))));
            this.stNodePropertyGrid1.ForeColor = System.Drawing.Color.White;
            this.stNodePropertyGrid1.ItemHoverColor = System.Drawing.Color.FromArgb(((int)(((byte)(50)))), ((int)(((byte)(125)))), ((int)(((byte)(125)))), ((int)(((byte)(125)))));
            this.stNodePropertyGrid1.ItemValueBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(80)))));
            this.stNodePropertyGrid1.Location = new System.Drawing.Point(0, 0);
            this.stNodePropertyGrid1.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.stNodePropertyGrid1.MinimumSize = new System.Drawing.Size(180, 75);
            this.stNodePropertyGrid1.Name = "stNodePropertyGrid1";
            this.stNodePropertyGrid1.ShowTitle = true;
            this.stNodePropertyGrid1.Size = new System.Drawing.Size(252, 139);
            this.stNodePropertyGrid1.TabIndex = 1;
            this.stNodePropertyGrid1.Text = "stNodePropertyGrid1";
            this.stNodePropertyGrid1.TitleColor = System.Drawing.Color.FromArgb(((int)(((byte)(127)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            // 
            // txt_log
            // 
            this.txt_log.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txt_log.Location = new System.Drawing.Point(0, 0);
            this.txt_log.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txt_log.Multiline = true;
            this.txt_log.Name = "txt_log";
            this.txt_log.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.txt_log.Size = new System.Drawing.Size(252, 159);
            this.txt_log.TabIndex = 0;
            this.txt_log.WordWrap = false;
            // 
            // panel1
            // 
            this.panel1.AutoScroll = true;
            this.panel1.AutoScrollMinSize = new System.Drawing.Size(4000, 3600);
            this.panel1.AutoSize = true;
            this.panel1.Controls.Add(this.stNodeEditor1);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(873, 841);
            this.panel1.TabIndex = 2;
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer2.IsSplitterFixed = true;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.splitContainer2.Name = "splitContainer2";
            this.splitContainer2.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.textBox1);
            this.splitContainer2.Panel1.Controls.Add(this.checkBox1);
            this.splitContainer2.Panel1.Controls.Add(this.btn_pause);
            this.splitContainer2.Panel1.Controls.Add(this.btn_new);
            this.splitContainer2.Panel1.Controls.Add(this.btn_stop);
            this.splitContainer2.Panel1.Controls.Add(this.btn_start);
            this.splitContainer2.Panel1.Controls.Add(this.btn_id_gen);
            this.splitContainer2.Panel1.Controls.Add(this.btn_unsubscribe);
            this.splitContainer2.Panel1.Controls.Add(this.tb_sn);
            this.splitContainer2.Panel1.Controls.Add(this.txt主题);
            this.splitContainer2.Panel1.Controls.Add(this.btn_subscribe);
            this.splitContainer2.Panel1.Controls.Add(this.btn_load);
            this.splitContainer2.Panel1.Controls.Add(this.button_save_as);
            this.splitContainer2.Panel1.Controls.Add(this.btn_save);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.splitContainer1);
            this.splitContainer2.Size = new System.Drawing.Size(1131, 927);
            this.splitContainer2.SplitterDistance = 80;
            this.splitContainer2.SplitterWidth = 6;
            this.splitContainer2.TabIndex = 3;
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(386, 70);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(76, 28);
            this.textBox1.TabIndex = 17;
            this.textBox1.Text = "1";
            // 
            // checkBox1
            // 
            this.checkBox1.AutoSize = true;
            this.checkBox1.Checked = true;
            this.checkBox1.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox1.Location = new System.Drawing.Point(714, 75);
            this.checkBox1.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.checkBox1.Name = "checkBox1";
            this.checkBox1.Size = new System.Drawing.Size(160, 22);
            this.checkBox1.TabIndex = 16;
            this.checkBox1.Text = "自动生成流水号";
            this.checkBox1.UseVisualStyleBackColor = true;
            // 
            // btn_pause
            // 
            this.btn_pause.Location = new System.Drawing.Point(714, 70);
            this.btn_pause.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btn_pause.Name = "btn_pause";
            this.btn_pause.Size = new System.Drawing.Size(112, 34);
            this.btn_pause.TabIndex = 15;
            this.btn_pause.Text = "暂停流程";
            this.btn_pause.UseVisualStyleBackColor = true;
            this.btn_pause.Visible = false;
            this.btn_pause.Click += new System.EventHandler(this.btn_pause_Click);
            // 
            // btn_new
            // 
            this.btn_new.Location = new System.Drawing.Point(18, 15);
            this.btn_new.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btn_new.Name = "btn_new";
            this.btn_new.Size = new System.Drawing.Size(112, 34);
            this.btn_new.TabIndex = 14;
            this.btn_new.Text = "新建";
            this.btn_new.UseVisualStyleBackColor = true;
            this.btn_new.Click += new System.EventHandler(this.btn_new_Click);
            // 
            // btn_stop
            // 
            this.btn_stop.Location = new System.Drawing.Point(592, 70);
            this.btn_stop.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btn_stop.Name = "btn_stop";
            this.btn_stop.Size = new System.Drawing.Size(112, 34);
            this.btn_stop.TabIndex = 13;
            this.btn_stop.Text = "停止流程";
            this.btn_stop.UseVisualStyleBackColor = true;
            this.btn_stop.Click += new System.EventHandler(this.btn_stop_Click);
            // 
            // btn_start
            // 
            this.btn_start.Location = new System.Drawing.Point(471, 70);
            this.btn_start.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btn_start.Name = "btn_start";
            this.btn_start.Size = new System.Drawing.Size(112, 34);
            this.btn_start.TabIndex = 13;
            this.btn_start.Text = "开始流程";
            this.btn_start.UseVisualStyleBackColor = true;
            this.btn_start.Click += new System.EventHandler(this.btn_start_Click);
            // 
            // btn_id_gen
            // 
            this.btn_id_gen.Location = new System.Drawing.Point(12, 68);
            this.btn_id_gen.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btn_id_gen.Name = "btn_id_gen";
            this.btn_id_gen.Size = new System.Drawing.Size(112, 34);
            this.btn_id_gen.TabIndex = 12;
            this.btn_id_gen.Text = "生成流水号";
            this.btn_id_gen.UseVisualStyleBackColor = true;
            this.btn_id_gen.Click += new System.EventHandler(this.btn_id_gen_Click);
            // 
            // btn_unsubscribe
            // 
            this.btn_unsubscribe.Location = new System.Drawing.Point(783, 15);
            this.btn_unsubscribe.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btn_unsubscribe.Name = "btn_unsubscribe";
            this.btn_unsubscribe.Size = new System.Drawing.Size(112, 34);
            this.btn_unsubscribe.TabIndex = 12;
            this.btn_unsubscribe.Text = "退订";
            this.btn_unsubscribe.UseVisualStyleBackColor = true;
            this.btn_unsubscribe.Click += new System.EventHandler(this.btn_unsubscribe_Click);
            // 
            // tb_sn
            // 
            this.tb_sn.Location = new System.Drawing.Point(134, 70);
            this.tb_sn.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.tb_sn.Name = "tb_sn";
            this.tb_sn.Size = new System.Drawing.Size(240, 28);
            this.tb_sn.TabIndex = 11;
            // 
            // txt主题
            // 
            this.txt主题.Location = new System.Drawing.Point(512, 16);
            this.txt主题.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txt主题.Name = "txt主题";
            this.txt主题.Size = new System.Drawing.Size(128, 28);
            this.txt主题.TabIndex = 11;
            this.txt主题.Text = "topic1";
            // 
            // btn_subscribe
            // 
            this.btn_subscribe.Location = new System.Drawing.Point(662, 15);
            this.btn_subscribe.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btn_subscribe.Name = "btn_subscribe";
            this.btn_subscribe.Size = new System.Drawing.Size(112, 34);
            this.btn_subscribe.TabIndex = 10;
            this.btn_subscribe.Text = "订阅";
            this.btn_subscribe.UseVisualStyleBackColor = true;
            this.btn_subscribe.Click += new System.EventHandler(this.btn_subscribe_Click);
            // 
            // btn_load
            // 
            this.btn_load.Location = new System.Drawing.Point(134, 15);
            this.btn_load.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btn_load.Name = "btn_load";
            this.btn_load.Size = new System.Drawing.Size(112, 34);
            this.btn_load.TabIndex = 0;
            this.btn_load.Text = "加载";
            this.btn_load.UseVisualStyleBackColor = true;
            this.btn_load.Click += new System.EventHandler(this.btn_load_Click);
            // 
            // button_save_as
            // 
            this.button_save_as.Location = new System.Drawing.Point(368, 15);
            this.button_save_as.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.button_save_as.Name = "button_save_as";
            this.button_save_as.Size = new System.Drawing.Size(112, 34);
            this.button_save_as.TabIndex = 0;
            this.button_save_as.Text = "另存为";
            this.button_save_as.UseVisualStyleBackColor = true;
            this.button_save_as.Click += new System.EventHandler(this.button_save_as_Click);
            // 
            // btn_save
            // 
            this.btn_save.Location = new System.Drawing.Point(249, 15);
            this.btn_save.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btn_save.Name = "btn_save";
            this.btn_save.Size = new System.Drawing.Size(112, 34);
            this.btn_save.TabIndex = 0;
            this.btn_save.Text = "保存";
            this.btn_save.UseVisualStyleBackColor = true;
            this.btn_save.Click += new System.EventHandler(this.btn_save_Click);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.ImageScalingSize = new System.Drawing.Size(32, 32);
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.delToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(117, 34);
            // 
            // delToolStripMenuItem
            // 
            this.delToolStripMenuItem.Name = "delToolStripMenuItem";
            this.delToolStripMenuItem.Size = new System.Drawing.Size(116, 30);
            this.delToolStripMenuItem.Text = "删除";
            this.delToolStripMenuItem.Click += new System.EventHandler(this.delToolStripMenuItem_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1131, 927);
            this.Controls.Add(this.splitContainer2);
            this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Name = "Form1";
            this.Text = "Form1";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.Load += new System.EventHandler(this.Form1_Load);
            this.SizeChanged += new System.EventHandler(this.Form1_SizeChanged);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.splitContainer3.Panel1.ResumeLayout(false);
            this.splitContainer3.Panel2.ResumeLayout(false);
            this.splitContainer3.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).EndInit();
            this.splitContainer3.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel1.PerformLayout();
            this.splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private ST.Library.UI.NodeEditor.STNodeTreeView stNodeTreeView1;
        private ST.Library.UI.NodeEditor.STNodeEditor stNodeEditor1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private ST.Library.UI.NodeEditor.STNodePropertyGrid stNodePropertyGrid1;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.Button btn_load;
        private System.Windows.Forms.Button btn_save;
        private System.Windows.Forms.SplitContainer splitContainer3;
        private System.Windows.Forms.TextBox txt_log;
        private System.Windows.Forms.Button btn_unsubscribe;
        private System.Windows.Forms.TextBox txt主题;
        private System.Windows.Forms.Button btn_subscribe;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem delToolStripMenuItem;
        private System.Windows.Forms.Button btn_start;
        private System.Windows.Forms.Button btn_new;
        private System.Windows.Forms.Button btn_pause;
        private System.Windows.Forms.Button btn_stop;
        private System.Windows.Forms.TextBox tb_sn;
        private System.Windows.Forms.Button btn_id_gen;
        private System.Windows.Forms.CheckBox checkBox1;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button button_save_as;
    }
}

