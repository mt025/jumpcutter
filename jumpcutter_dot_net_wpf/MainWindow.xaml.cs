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
        DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
        StringWriter log = new StringWriter();
        public MainWindow()
        {
            InitializeComponent();
            log = new StringWriter();
            dispatcherTimer.Tick += dispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();

        }

        private void LoadVideoBtn_Click(object sender, RoutedEventArgs e)
        {

            OpenFileDialog fileBrowser = new OpenFileDialog
            {
                Filter = "MP4 file (*.mp4)|*.mp4"
            };

            if (fileBrowser.ShowDialog() != true) return;


            Options options = new Options();
            var ops = Parser.Default.ParseArguments<Options>(new string[] { "--input_file", fileBrowser.FileName }).WithParsed(x => options = x);



            // You can also use an anonymous delegate to do this.
            Thread t2 = new Thread(delegate ()
            {
                var jc = new JumpCutter(options, log);
                jc.Process();
            });
            t2.Start();




        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            log.Flush();
            if (string.IsNullOrEmpty(log.ToString())) return;
            
            var splitData = log.ToString().Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);



            for (var i = splitData.Length - 1; i >= 0; i--)
            {
                var returnString = splitData[i].StartsWith("\r");
                if (!returnString || i == splitData.Length - 1)
                {
                    if (lstConsole.Items.Count > 0)
                    {
                        var lastItem = lstConsole.Items.GetItemAt(lstConsole.Items.Count - 1);
                        
                        if (lastItem.ToString().StartsWith(Char.MinValue.ToString()))
                        {
                            lstConsole.Items.Remove(lastItem);
                        }
                    }

                }
                var substr = splitData[i].Split(new string[] { "\r" }, StringSplitOptions.RemoveEmptyEntries);
                string finalx;
                if (substr.Length > 1)
                {
                    finalx  = Char.MinValue + substr.Last();
                }
                else
                {
                    finalx = substr.Last();
                }
                lstConsole.Items.Add(finalx);
                



            }

            log.GetStringBuilder().Clear();

        }
    }
}
