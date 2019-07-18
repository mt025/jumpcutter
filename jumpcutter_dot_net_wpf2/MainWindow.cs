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
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace jumpcutter_dot_net_wpf2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Options options;
        JumpCutter jc;
        public MainWindow()
        {
            //NativeMethods.AllocConsole();
            options = new Options();
            InitializeComponent();


            //processor.PlaybackStopped += EventHandler_playbackStopped;
        }

        // Open mp4 file for processing
        private void OpenFile(string fileName)
        {
            Stop();
            var cmdLineOpts = new string[] { "--input_file", fileName };
            var ops = Parser.Default.ParseArguments<Options>(cmdLineOpts);
            ops.WithParsed(x => options = x);


            jc = new JumpCutter(ref options);

            var videoProcessor = new Thread(delegate () {
                jc.WPFStageVideo();
                jc.WPFStageAudio();
                jc.audioProcessor.prepareStream();
                jc.audioProcessor.stream.AudioFrameRendered += new JumpCutterStreamProcessor.AudioFrameHandler(renderFrame);



            });



            var processAudio = new Thread(delegate ()
            {
                videoProcessor.Start();
                //Wait for the video processor to init and handle the callback to render a frame
                videoProcessor.Join();
                jc.audioProcessor.Stream();


            });
            processAudio.Start();




            //  if (//processor.OpenWavFile(jc.options.temp_audio ) == true)
            // {
            //     button_play.IsEnabled = true;
            //     button_stop.IsEnabled = true;
            //
            //     // Parse adjustment settings
            //     SetTempo(0);
            // }
            // else
            // {
            //     button_play.IsEnabled = false;
            //     button_stop.IsEnabled = false;
            //     MessageBox.Show("Coudln't open audio file " + fileName);
            // }
        }

        private BitmapImage ToBitmapImage(Bitmap bitmap)
        {
            using (var memory = new MemoryStream())
            {
                //var format = bitmap.RawFormat;
                var format = ImageFormat.Jpeg;
                bitmap.Save(memory, format);
                memory.Position = 0;

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                return bitmapImage;
            }
        }

        private bool frameRendering = false;
        //This runs on the JC thread
        private void renderFrame()
        {
            if (frameRendering)
                return;



            this.Dispatcher.Invoke(() =>
            {
               // text_status.Text = ("Frame Rendered" + DateTime.Now.Ticks.ToString());

            });


                try
                {
                frameRendering = true;
                var vp = jc.videoProcessor;
                    var image = ToBitmapImage(vp.getNextFrame().Bitmap);
                frameRendering = false;
                Dispatcher.Invoke(new Action(() => {
                    var image2 = image;
                    videoArea.Source = image2;

                }), DispatcherPriority.ContextIdle);
            }
                catch (Exception) { }



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
                //// Pause
                //if (//processor.Pause())
                //{
                //    text_status.Text = "Paused";
                //}
                //SetPlayButtonMode(true);
            }
            else
            {
                // Play
                // if (//processor.Play())
                // {
                //     text_status.Text = "Playing";
                //     SetPlayButtonMode(false);
                // }
            }  //
        }


        private void Stop()
        {
            // if (//processor.Stop())
            // {
            //     text_status.Text = "Stopped";
            // }
            // SetPlayButtonMode(true);
        }


        private void Button_stop_Click(object sender, RoutedEventArgs e)
        {
            Stop();
        }

        //  Handler for file drag & drop over the window
        private void Window_Drop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            // open 1st of the chosen files
            OpenFile(files[0]);
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            options.sounded_speed = speedSlider.Value;
        }

        private void SSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            options.silent_speed = sSpeedSlider.Value;
        }
    }
}
