//////////////////////////////////////////////////////////////////////////////
///
/// C# example that manipulates mp3 audio files with SoundTouch library.
/// 
/// Author        : Copyright (c) Olli Parviainen
/// Author e-mail : oparviai 'at' iki.fi
/// SoundTouch WWW: http://www.surina.net/soundtouch
///
////////////////////////////////////////////////////////////////////////////////
//
// License for this source code file: Microsoft Public License(Ms-PL)
//
////////////////////////////////////////////////////////////////////////////////

using CommandLine;
using Jumpcutter_dot_net;
using SoundTouch;
using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace jumpcutter_dot_net_wpf2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        protected SoundProcessor processor = new SoundProcessor();

        public MainWindow()
        {
            NativeMethods.AllocConsole();
            InitializeComponent();

            StatusMessage.StatusEvent += StatusEventHandler;
            processor.PlaybackStopped += EventHandler_playbackStopped;
        }


        private void StatusEventHandler(object sender, string msg)
        {
            text_status.Text = msg;
        }


        // Open mp4 file for processing
        private void OpenFile(string fileName)
        {
            Stop();

            Options options = new Options();
            var ops = Parser.Default.ParseArguments<Options>(new string[] { "--input_file", fileName }).WithParsed(x => options = x);

            var jc = new JumpCutter(options);
            //jumpcutter = new Thread(delegate ()
            //{

            jc.WPFStage1();
            //});
            //jumpcutter.Start();

            if (processor.OpenWavFile(jc.options.temp_audio ) == true)
            {
                button_play.IsEnabled = true;
                button_stop.IsEnabled = true;

                // Parse adjustment settings
                SetTempo(0);
            }
            else
            {
                button_play.IsEnabled = false;
                button_stop.IsEnabled = false;
                MessageBox.Show("Coudln't open audio file " + fileName);
            }
        }


        private void Button_browse_Click(object sender, RoutedEventArgs e)
        {
            // Show file selection dialog
            Microsoft.Win32.OpenFileDialog openDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "MP4 files (*.mp4)|*.mp4"
            };
            if (openDialog.ShowDialog() == true)
            {
                OpenFile(openDialog.FileName);
            }
        }


        private void SetPlayButtonMode(bool play)
        {
            button_play.Content = play ? "_Play" : "_Pause";
        }


        private void EventHandler_playbackStopped(object sender, bool hasReachedEnd)
        {
            if (hasReachedEnd)
            {
                text_status.Text = "Stopped";
            }   // otherwise paused

            SetPlayButtonMode(true);
        }


        private void Button_play_Click(object sender, RoutedEventArgs e)
        {
            if ((string)button_play.Content == "_Pause")
            {
                // Pause
                if (processor.Pause())
                {
                    text_status.Text = "Paused";
                }
                SetPlayButtonMode(true);
            }
            else
            {
                // Play
                if (processor.Play())
                {
                    text_status.Text = "Playing";
                    SetPlayButtonMode(false);
                }
            }
        }


        private void Stop()
        {
            if (processor.Stop())
            {
                text_status.Text = "Stopped";
            }
            SetPlayButtonMode(true);
        }


        private void Button_stop_Click(object sender, RoutedEventArgs e)
        {
            Stop();
        }

        private void SetTempo(float tempo)
        {

            if (processor.streamProcessor != null) processor.streamProcessor.st.SetTempoChange(tempo);
        }


        //  Handler for file drag & drop over the window
        private void Window_Drop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            // open 1st of the chosen files
            OpenFile(files[0]);
        }
    }
}
