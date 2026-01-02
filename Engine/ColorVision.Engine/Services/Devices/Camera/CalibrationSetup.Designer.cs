
namespace WindowsFormsTest
{
    partial class CalibrationSetup
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
            this.btn_cur = new System.Windows.Forms.Button();
            this.btn_def = new System.Windows.Forms.Button();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.btn_apply = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.SuspendLayout();
            // 
            // btn_cur
            // 
            this.btn_cur.Location = new System.Drawing.Point(110, 12);
            this.btn_cur.Name = "btn_cur";
            this.btn_cur.Size = new System.Drawing.Size(92, 23);
            this.btn_cur.TabIndex = 4;
            this.btn_cur.Text = "获取当前参数";
            this.btn_cur.UseVisualStyleBackColor = true;
            this.btn_cur.Click += new System.EventHandler(this.btn_cur_Click);
            // 
            // btn_def
            // 
            this.btn_def.Location = new System.Drawing.Point(12, 12);
            this.btn_def.Name = "btn_def";
            this.btn_def.Size = new System.Drawing.Size(92, 23);
            this.btn_def.TabIndex = 5;
            this.btn_def.Text = "获取默认参数";
            this.btn_def.UseVisualStyleBackColor = true;
            this.btn_def.Click += new System.EventHandler(this.btn_def_Click);
            // 
            // dataGridView1
            // 
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Location = new System.Drawing.Point(12, 41);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.RowTemplate.Height = 23;
            this.dataGridView1.Size = new System.Drawing.Size(712, 278);
            this.dataGridView1.TabIndex = 6;
            // 
            // btn_apply
            // 
            this.btn_apply.Location = new System.Drawing.Point(220, 12);
            this.btn_apply.Name = "btn_apply";
            this.btn_apply.Size = new System.Drawing.Size(92, 23);
            this.btn_apply.TabIndex = 7;
            this.btn_apply.Text = "应用";
            this.btn_apply.UseVisualStyleBackColor = true;
            this.btn_apply.Click += new System.EventHandler(this.btn_apply_Click);
            // 
            // CalibrationSetup
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(736, 331);
            this.Controls.Add(this.btn_apply);
            this.Controls.Add(this.dataGridView1);
            this.Controls.Add(this.btn_def);
            this.Controls.Add(this.btn_cur);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Name = "CalibrationSetup";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "CalibrationSetup";
            this.Load += new System.EventHandler(this.CameraSetup_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Button btn_cur;
        private System.Windows.Forms.Button btn_def;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Button btn_apply;
    }
}