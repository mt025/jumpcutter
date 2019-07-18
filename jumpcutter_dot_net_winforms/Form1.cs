using CommandLine;
using Jumpcutter_dot_net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace jumpcutter_dot_net_winforms
{
    public partial class Form1 : Form
    {
        JumpCutter jc;
        bool currentSpeedNormal = true;


        public Form1()
        {




            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            // Show file selection dialog

            if (openFileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            vlcControl1.SetMedia(new FileInfo(openFileDialog.FileName));
            vlcControl1.Play();
            vlcControl1.Rate = 2;

        }


      private float speedBar
        {
            get { return tbSounded.Value / 100; }
            set { tbSounded.Value = (int)(value * 100); }

        }

        private float slowBar
        {
            get { return tbSilent.Value / 100; }
            set { tbSilent.Value = (int)(value * 100); }

        
    }
}
