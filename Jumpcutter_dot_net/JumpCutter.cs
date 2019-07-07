using System.IO;
using System;
using System.Globalization;
using Emgu.CV;
using Emgu.CV.UI;
using Emgu.CV.Structure;
using Emgu.Util;
using System.Collections.Generic;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Model;

namespace Jumpcutter_dot_net
{
    internal class JumpCutter
    {
        private Arguments options;
        private FileInfo videoInputFile;
        private FileInfo videoOutputFile;

        private VideoCapture inputVideo;
        private VideoWriter outputVideo;

        private string tempVideo;
        private string tempAudio;

        public JumpCutter(Arguments options)
        {
            this.options = options;


            //Download FFMPEG
            FFmpeg.GetLatestVersion().Wait();

            //Check the input file and output file
            handleInputOutputAndTemp();

            //Init the video object
            inputVideo = new VideoCapture(videoInputFile.FullName);

            //Get the video framerate 
            Console.WriteLine("Getting Video Data...");
            if (options.frame_rate == null) getVideoFrameData();

            //Process the audio
            var ignoredFrames = processAudio();

            //Init output video
            using (outputVideo = new VideoWriter(tempVideo, options.video_codec, (double)options.frame_rate, options.frame_size, true))
            {
                outputVideo.Set(VideoWriter.WriterProperty.Quality, options.frame_quality / 100.00);
                writeFinalVideo(ignoredFrames, "");
            }

            addAudioToVideo();

        }

        private List<int> processAudio()
        {

            ///TODO write progress
            //Get the audio file from the video
            Console.WriteLine("Extracting audio...");
            var conv = Conversion.ExtractAudio(videoInputFile.FullName, tempAudio);
            conv.Start().Wait();
            Console.WriteLine("Audio Extracted.");

            HashSet<int> framesToDrop = new HashSet<int>();
            var random = new Random();

            //Drop 500% of random frames
            for (var i = 0; i < options.frame_count * 5; i += 1)
            {
                //framesToDrop.Add(random.Next(1, options.frame_count));
            }

            return new List<int>(framesToDrop);


        }



        private void writeFinalVideo(List<int> ignoreFrames, string audioFile)
        {

            Console.WriteLine("Building video...");

            //Update status every x frames, with a 0.1% rate
            var updateStatus = options.frame_count / 1000;

            for (var i = 1; i <= options.frame_count; i += 1)
            {

                if (i % updateStatus == 0)
                {
                    var pc = ((double)i / options.frame_count * 100);
                    Console.Write("\rWriting frame " + i + " out of " + options.frame_count + " (" + pc.ToString("0.00") + "%)");
                }

                //move to next frame
                var nextFrame = inputVideo.Grab();


                //Even though frame is in frame_count, it sometimes doesn't exsit?
                if (!nextFrame) continue;

                if (!ignoreFrames.Contains(i))
                {

                    var img = new Mat();
                    inputVideo.Retrieve(img);

                    outputVideo.Write(img);
                    img.Dispose();
                }
                else
                {

                }

            }

            Console.WriteLine("\rWriting frame " + options.frame_count + " out of " + options.frame_count + " (100.00%)");
        }

        private void addAudioToVideo()
        {
            ///TODO write progress
            Console.WriteLine("Joining video and audio...");
            //Concat the audio and video
            var tempAudio = options.temp_dir + @"\" + "fullaudio.wav";

            var conv = Conversion.AddAudio(tempVideo, tempAudio, videoOutputFile.FullName);
            conv.Start().Wait();

        }

        public void handleInputOutputAndTemp()
        {

            //Does the extention end .mp4, if so we will assume the file is a mp4 file
            if (!options.input_file.EndsWith(".mp4", true, CultureInfo.CurrentCulture))
            {
                throw new JCException("This application only supports MP4 as an input file");
            }

            //Open the input file
            videoInputFile = new FileInfo(options.input_file);
            //Does it exist?
            if (!videoInputFile.Exists) throw new JCException("File " + options.input_file + " doesn't exist");

            if (options.output_file != null)
            {
                if (!options.input_file.EndsWith(".mp4", true, CultureInfo.CurrentCulture)) throw new JCException("This application only supports MP4 as an output file");

                videoOutputFile = new FileInfo(options.output_file);

            }
            else
            {
                videoOutputFile = new FileInfo(videoInputFile.DirectoryName + "/" + Path.GetFileNameWithoutExtension(videoInputFile.Name) + "_ALTERED.mp4");
            }

            if (videoOutputFile.Exists)
            {
                ///TODO Add param for overrite file
                if (true)
                {
                    videoOutputFile.Delete();
                }
                else
                {
                    throw new JCException("File " + options.output_file + " already exists");
                }

            }

            try
            {
                ///TODO FIX THIS
                //Make sure we can create and write the file
                videoOutputFile.Directory.Create();
                //videoOutputFile.Create();
                //videoOutputFile.Delete();
            }
            catch (Exception e) { throw new JCException("Unable to create output file " + options.output_file + "! " + e.Message); }

            var tempdir = new DirectoryInfo(videoOutputFile.Directory + @"\" + videoInputFile.Name + "_temp");
            options.temp_dir = tempdir.FullName;
            if (tempdir.Exists)
            {
                tempdir.Delete(true);
            }

            tempdir.Create();
            tempVideo = options.temp_dir + @"\" + "video_no_audio.mp4";
            tempAudio = options.temp_dir + @"\" + "fullaudio.wav";
        }

        void getVideoFrameData()
        {

            var frameRate = inputVideo.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.Fps);
            var frameWidth = (int)inputVideo.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameWidth);
            var frameHeight = (int)inputVideo.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameHeight);
            var frameCount = (int)(inputVideo.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameCount)); ;
            var videoLength = (int)(frameCount / frameRate);

            //Just to avoid numbers like 29.99999954
            frameRate = Math.Round(frameRate, 4);

            options.frame_rate = frameRate;
            options.frame_count = frameCount;
            options.orignial_length = videoLength;
            options.frame_size = new System.Drawing.Size(frameWidth, frameHeight);

            //Only need to do this to have the same input codec as output codec, we will be using MP4 so we omit this
            //var codec = (int)(inputVideo.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FourCC));
            //options.video_codec = codec;


            Console.WriteLine("Frame Rate Detected as: " + options.frame_rate);
            Console.WriteLine("Duration Detected as: " + options.orignial_length);

        }



        //This function is slower than streaming the whole file and dropping un-needed frames
        //private Mat getFrameAt(int frameNumber)
        //{
        //
        //    inputVideo.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.PosFrames, frameNumber);
        //    return inputVideo.QueryFrame();
        //}

    }
}