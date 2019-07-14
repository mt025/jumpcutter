using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using CommandLine;
using Jumpcutter_dot_net;
using Microsoft.Win32;


namespace jumpcutter_dot_net_wpf
{
    public partial class MainWindow : Window
    {
        Thread jumpcutter;
        public MainWindow()
        {

            NativeMethods.AllocConsole();
            InitializeComponent();
        }

        private void LoadVideoBtn_Click(object sender, RoutedEventArgs e)
        {

            OpenFileDialog fileBrowser = new OpenFileDialog
            {
                Filter = "MP4 file (*.mp4)|*.mp4"
            };

            if (fileBrowser.ShowDialog() != true) return;

            btnLoadVideo.IsEnabled = false;

            Options options = new Options();
            var ops = Parser.Default.ParseArguments<Options>(new string[] { "--input_file", fileBrowser.FileName }).WithParsed(x => options = x);
            var jc = new JumpCutter(options);
           //jumpcutter = new Thread(delegate ()
           //{
               
                jc.WPFStage1();
            //});
            //jumpcutter.Start();

           
        }

        ~MainWindow() {
            jumpcutter.Abort();
        }



    }
}
