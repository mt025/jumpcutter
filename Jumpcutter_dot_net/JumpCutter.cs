using System.IO;
using System;
using System.Globalization;
using Emgu.CV;
using Emgu.CV.UI;
using Emgu.CV.Structure;
using Emgu.Util;




namespace Jumpcutter_dot_net
{
    internal class JumpCutter
    {
        private Arguments options;
        private FileInfo videoInputFile;
        private FileInfo videoOutputFile;
        private VideoCapture inputVideo;
        private VideoWriter outputVideo;

        public JumpCutter(Arguments options)
        {
            this.options = options;


            //Check the input file and output file
            checkFilesReadAndWrite();

            //Init the video object
            inputVideo = new VideoCapture(videoInputFile.FullName);

            //Get the video framerate 
            if (options.frame_rate == null){
                getVideoFrameData();
            }

            //Init output video
            using (outputVideo = new VideoWriter(videoOutputFile.FullName, options.video_codec, (double)options.frame_rate, options.frame_size, true))
            {
                
                test();

            }

            


        }

        private void test()
        {
            for (var i = 1; i <= options.frame_count; i += 1)
            {
                using (var frame = getFrameAt(i)) {
                //Evem though frame is in frame_count, it doesn't exsit?
                if (frame.Bitmap == null) continue;
                outputVideo.Write(frame);
                }
            }


            //CvInvoke.Imshow("Image",frame);
            //CvInvoke.WaitKey(0);

        }

        private Mat getFrameAt(int frameNumber) {

            inputVideo.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.PosFrames, frameNumber);
            return inputVideo.QueryFrame();
        }

        public void checkFilesReadAndWrite()
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

            try { videoOutputFile.Directory.Create(); //videoOutputFile.Create(); 
            } catch (Exception e) { throw new JCException("Unable to create output file " + options.output_file + "! " + e.Message); }



        }

        void getVideoFrameData()
        {

            var frameRate = inputVideo.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.Fps);
            var frameWidth = (int) inputVideo.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameWidth);
            var frameHeight = (int) inputVideo.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameHeight);
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




    }
}