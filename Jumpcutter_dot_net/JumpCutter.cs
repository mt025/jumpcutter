using System.IO;
using System;
using System.Globalization;
using Emgu.CV;
using System.Collections.Generic;
using Xabe.FFmpeg;
using NAudio.Wave;
using System.Linq;
namespace Jumpcutter_dot_net
{
    public class JumpCutter
    {
        public readonly Options options;

        private AudioProcessor audioProcessor;
        private VideoProcessor videoProcessor;

        FileInfo videoInputFile;
        public FileInfo lockfile;


        public JumpCutter(Options options,TextWriter tw = null)
        {
            if(tw != null)
            {
                Console.SetOut(tw);
                Console.SetError(tw);
            }

            this.options = options;

        }

        public void Process()
        {

            //Check the input file and output file
            HandleInputOutputTempFiles();

            //Init the audio processor
            audioProcessor = new AudioProcessor(options);

            //Init the video prosessor
            videoProcessor = new VideoProcessor(options);

            //Download FFMPeg
            Console.WriteLine("Getting Latest FFmpeg...");
            videoProcessor.DownloadFFMpeg();
            Console.WriteLine();

            //Get the video data
            Console.WriteLine("Getting Video Data... ");
            videoProcessor.GetVideoFrameData();
            Console.WriteLine();

            //Prepare the audio
            Console.WriteLine("Extracting Audio...");
            audioProcessor.PrepareAudio();
            Console.WriteLine();

            //Process the audio
            Console.WriteLine("Processing Audio...");
            var framesToRender = audioProcessor.WriteAudio();
            Console.WriteLine();

            //Process the video
            Console.WriteLine("Processing video...");
            videoProcessor.WriteFinalVideo(framesToRender, "");
            Console.WriteLine();

            //Join video and audio
            Console.WriteLine("Joining video and audio...");
            videoProcessor.AddAudioToVideo();
            Console.WriteLine();


            //Join video and audio
            if (!options.keep_orignal)
            {
                Console.WriteLine("Deleting orignal video...");
                videoInputFile.Delete();
                Console.WriteLine();
            }

            lockfile.Delete();

        }

        public void WPFStage1()
        {

            //Check the input file and output file
            HandleInputOutputTempFiles();

            //Init the audio processor
            audioProcessor = new AudioProcessor(options);

            //Init the video prosessor
            videoProcessor = new VideoProcessor(options);

            //Download FFMPeg
            Console.WriteLine("Getting Latest FFmpeg...");
            videoProcessor.DownloadFFMpeg();
            Console.WriteLine();

            //Get the video data
            Console.WriteLine("Getting Video Data... ");
            videoProcessor.GetVideoFrameData();
            Console.WriteLine();

            //Prepare the audio
            Console.WriteLine("Extracting Audio...");
            audioProcessor.PrepareAudio();
            Console.WriteLine();
        }



        private void HandleInputOutputTempFiles()
        {
            FileInfo videoOutputFile;


            //Does the extention end .mp4, if so we will assume the file is a mp4 file
            if (!options.input_file.EndsWith(".mp4", true, CultureInfo.CurrentCulture))
            {
                throw new JCException("This application only supports MP4 as an input file");
            }

            //Open the input file
            videoInputFile = new FileInfo(options.input_file);
            //Does it exist?
            if (!videoInputFile.Exists) throw new JCException("File " + options.input_file + " doesn't exist");

            lockfile = new FileInfo(videoInputFile.Directory + @"\" + videoInputFile.Name + ".lock");
            if (lockfile.Exists && lockfile.Name != "jctestfilejc.mp4.lock")
            {
                //We are already processing this file
                throw new FileLoadException("File is locked for processing by " + lockfile.Name);
            }
            else
            {
                lockfile.Create();
            }


            //Setup output file
            if (options.output_file != null)
            {
                //Does the extention end .mp4, if so we will assume the file is a mp4 file
                if (!options.input_file.EndsWith(".mp4", true, CultureInfo.CurrentCulture)) throw new JCException("This application only supports MP4 as an output file");

                videoOutputFile = new FileInfo(options.output_file);

            }
            else
            {
                videoOutputFile = new FileInfo(videoInputFile.DirectoryName + "/" + Path.GetFileNameWithoutExtension(videoInputFile.Name) + "_ALTERED.mp4");
                options.output_file = videoOutputFile.FullName;
            }


            



            //If the output file already exists, delete it, if debug mode
            if (videoOutputFile.Exists)
            {
                ///TODO Add param for overrite file
                if (options.overwrite_output)
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
                //Make sure we can create and write the file
                videoOutputFile.Directory.Create();
                videoOutputFile.Directory.GetAccessControl();
            }
            catch (Exception e) { throw new JCException("Unable to create output file " + options.output_file + "! ", e); }

            var tempdir = new DirectoryInfo(videoOutputFile.Directory + @"\" + videoInputFile.Name + "_temp");
            options.temp_dir = tempdir.FullName;

            //Clear the temp dir
            if (tempdir.Exists)
            {
                tempdir.Delete(true);
            }

            tempdir.Create();
        }


    }
}