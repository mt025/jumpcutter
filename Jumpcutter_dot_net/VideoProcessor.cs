using Emgu.CV;
using System;
using System.Collections.Generic;
using System.IO;
using Xabe.FFmpeg;

namespace Jumpcutter_dot_net
{
    public class VideoProcessor
    {
        private readonly Options options;
        private readonly Utils utils;

        private readonly VideoCapture inputVideo;
        private VideoWriter outputVideo;
        private readonly string tempVideo;


        public VideoProcessor(ref Options options)
        {
            this.options = options;
            this.utils = new Utils();
            //Init the video object
            inputVideo = new VideoCapture(options.input_file);
            tempVideo = options.temp_dir + @"\" + "video_no_audio.mp4";

        }

        internal void GetVideoFrameData()
        {
            if (options.frame_rate == null)
            {
                options.frame_rate = inputVideo.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.Fps);
            }


            var frameWidth = (int)inputVideo.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameWidth);
            var frameHeight = (int)inputVideo.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameHeight);
            var frameCount = (int)(inputVideo.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameCount)); ;
            var videoLength = (int)(frameCount / options.frame_rate);

            //Just to avoid numbers like 29.99999954
            //frameRate = Math.Round(frameRate, 4);


            options.frame_count = frameCount;
            options.orignial_length = videoLength;
            options.frame_size = new System.Drawing.Size(frameWidth, frameHeight);

            //Only need to do this to have the same input codec as output codec, we will be using MP4 so we omit this
            //var codec = (int)(inputVideo.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FourCC));
            //options.video_codec = codec;


            Console.WriteLine("\t" + options.frame_rate + " fps | " + options.orignial_length + " seconds | " + options.frame_count + " frames | " + options.frame_size.Height + "p");

        }

        internal void DownloadFFMpeg()
        {
            if (options.download_ffmpeg == true)
            {
                var lastDir = Directory.GetCurrentDirectory();
                var binDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                Directory.SetCurrentDirectory(binDir);
                //Download FFMPEG
                var ffmpeg = FFmpeg.GetLatestVersion();
                try
                {
                    ffmpeg.Wait();

                    if (ffmpeg.Status == System.Threading.Tasks.TaskStatus.RanToCompletion)
                    {
                        Console.WriteLine("\tFFMpeg is Up to date");
                    }
                    else
                    {
                        throw new JCException("Download FFMpeg Task Failed to complete");
                    }
                }
                catch (Exception e)
                {

                    ////TODO or disable it in the settings
                    throw new JCException("\tCould not download FFmpeg, please allow this application network access, or copy the ffmpeg.exe and ffprobe.exe to the bin dir", e);
                }
                finally
                {
                    Directory.SetCurrentDirectory(lastDir);
                }
            }
        }

        public Mat getNextFrame()
        {
            // var frame = inputVideo.QueryFrame();


            var nextFrame = inputVideo.Grab();
         

                if (!nextFrame) return null;


                var img = new Mat();
                inputVideo.Retrieve(img);


                return img;
          

        }

        internal void WriteFinalVideo(List<int> framesToRender, string audioFile)
        {

            using (outputVideo = new VideoWriter(tempVideo, Options.VIDEO_CODEC, (double)options.frame_rate, options.frame_size, true))
            {
                outputVideo.Set(VideoWriter.WriterProperty.Quality, options.frame_quality / 100.00);


                //Update status every x frames, with a 0.1% rate
                var lastFrame = 0;
                var count = 0;

                foreach (var frame in framesToRender)
                {
                    var adjframe = frame + 1;

                    utils.ReportStatus("Writing frame {0} out of {1} {2}", count, framesToRender.Count, 2);
                    count++;
                    if (adjframe != lastFrame)
                    {
                        var framesToMove = adjframe - lastFrame;
                        bool nextFrame = true;
                        //move to next x frame
                        for (var i = 0; i < framesToMove; i++)
                        {
                            nextFrame = inputVideo.Grab();
                        }

                        if (!nextFrame) return;

                        var img = new Mat();
                        inputVideo.Retrieve(img);

                        outputVideo.Write(img);
                        img.Dispose();


                    }
                    lastFrame = adjframe;

                }
                utils.ReportStatus("Writing frame {0} out of {1} {2}", options.frame_count, options.frame_count, 2, last: true);
            }
        }

        internal void AddAudioToVideo()
        {
            ///TODO write progress
            ///TODO move this away from FFMpeg to native

            //Concat the audio and video
            var tempAudio = options.temp_dir + @"\" + "finalAudio.wav";

            var conv = Conversion.AddAudio(tempVideo, tempAudio, options.output_file);
            conv.Start().Wait();

        }


        //This function is slower than streaming the whole file and dropping un-needed frames
        internal Mat GetFrameAt(int frameNumber)
        {

            inputVideo.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.PosFrames, frameNumber);
            return inputVideo.QueryFrame();
        }



    }
}