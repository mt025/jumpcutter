using System;
using System.IO;

namespace jumpcutter_dot_net_winforms
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        private DirectoryInfo vlcdir = new DirectoryInfo(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "libvlc", IntPtr.Size == 4 ? "win-x86" : "win-x64"));
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


            this.components = new System.ComponentModel.Container();
            this.button1 = new System.Windows.Forms.Button();
            this.tbSounded = new System.Windows.Forms.TrackBar();
            this.tbSilent = new System.Windows.Forms.TrackBar();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.stsLblMain = new System.Windows.Forms.ToolStripStatusLabel();
            this.openFileDialog = new System.Windows.Forms.OpenFileDialog();
            this.fpsTimer = new System.Windows.Forms.Timer(this.components);
            this.vlcControl1 = new Vlc.DotNet.Forms.VlcControl();
            ((System.ComponentModel.ISupportInitialize)(this.tbSounded)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbSilent)).BeginInit();
            this.statusStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.vlcControl1)).BeginInit();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(12, 400);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 1;
            this.button1.Text = "&Browse";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.BrowseButton_Click);
            // 
            // tbSounded
            // 
            this.tbSounded.Location = new System.Drawing.Point(93, 378);
            this.tbSounded.Maximum = 1000;
            this.tbSounded.Minimum = 1;
            this.tbSounded.Name = "tbSounded";
            this.tbSounded.Size = new System.Drawing.Size(695, 45);
            this.tbSounded.TabIndex = 2;
            this.tbSounded.Value = 100;
            // 
            // tbSilent
            // 
            this.tbSilent.Location = new System.Drawing.Point(93, 418);
            this.tbSilent.Maximum = 1000;
            this.tbSilent.Minimum = 1;
            this.tbSilent.Name = "tbSilent";
            this.tbSilent.Size = new System.Drawing.Size(695, 45);
            this.tbSilent.TabIndex = 3;
            this.tbSilent.Value = 100;
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.stsLblMain});
            this.statusStrip1.Location = new System.Drawing.Point(0, 453);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(800, 22);
            this.statusStrip1.TabIndex = 4;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // stsLblMain
            // 
            this.stsLblMain.Name = "stsLblMain";
            this.stsLblMain.Size = new System.Drawing.Size(0, 17);
            // 
            // openFileDialog
            // 
            this.openFileDialog.FileName = "openFileDialog";
            this.openFileDialog.Filter = "MP4 files (*.mp4)|*.mp4";
            // 
            // fpsTimer
            // 
            this.fpsTimer.Tick += new System.EventHandler(this.FpsTimer_Tick);
            // 
            // vlcControl1
            // 
            this.vlcControl1.BackColor = System.Drawing.Color.Black;
            this.vlcControl1.Location = new System.Drawing.Point(12, 12);
            this.vlcControl1.Name = "vlcControl1";
            this.vlcControl1.Size = new System.Drawing.Size(776, 360);
            this.vlcControl1.Spu = -1;
            this.vlcControl1.TabIndex = 5;
            this.vlcControl1.Text = "vlcControl1";
            this.vlcControl1.VlcLibDirectory = vlcdir;
            this.vlcControl1.VlcMediaplayerOptions = null;
            this.vlcControl1.Click += new System.EventHandler(this.VlcControl1_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 475);
            this.Controls.Add(this.vlcControl1);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.tbSilent);
            this.Controls.Add(this.tbSounded);
            this.Controls.Add(this.button1);
            this.Name = "Form1";
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.Form1_Load);
            ((System.ComponentModel.ISupportInitialize)(this.tbSounded)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tbSilent)).EndInit();
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.vlcControl1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.TrackBar tbSounded;
        private System.Windows.Forms.TrackBar tbSilent;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel stsLblMain;
        private System.Windows.Forms.OpenFileDialog openFileDialog;
        private System.Windows.Forms.Timer fpsTimer;
        private Vlc.DotNet.Forms.VlcControl vlcControl1;
    }
}

