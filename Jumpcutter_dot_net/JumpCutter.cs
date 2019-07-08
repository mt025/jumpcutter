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
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Linq;
using Varispeed.SoundTouch;

namespace Jumpcutter_dot_net
{
    internal class JumpCutter
    {
        private Options options;
        private FileInfo videoInputFile;
        private FileInfo videoOutputFile;

        private VideoCapture inputVideo;
        private VideoWriter outputVideo;

        private string tempVideo;
        private string tempAudio;



        public JumpCutter(Options options)
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

            //Prepare the audio
            prepareAudio();

            //Process the audio
            var ignoredFrames = processAudio();


            //Init output video
            using (outputVideo = new VideoWriter(tempVideo, Options.VIDEO_CODEC, (double)options.frame_rate, options.frame_size, true))
            {
                outputVideo.Set(VideoWriter.WriterProperty.Quality, options.frame_quality / 100.00);
                writeFinalVideo(ignoredFrames, "");
            }

            addAudioToVideo();

        }

        private List<int> processAudio()
        {

            float maxVolume = 0;
            List<bool> hasLoudAudio = new List<bool>();

            using (var audioFileReader = new AudioFileReader(tempAudio))
            {
                //Sample rate of the audio file
                var sampleRate = audioFileReader.WaveFormat.SampleRate;

                //Samples of audio per frame
                var samplesPerFrame = (int)((sampleRate * audioFileReader.WaveFormat.Channels) / (double)options.frame_rate);

                List<float> maxVolumePerFrame = new List<float>();

                //Get Max Volume for each frame
                float[] sampleBuffer = new float[samplesPerFrame];
                while (audioFileReader.Read(sampleBuffer, 0, samplesPerFrame) > 0)
                {
                    var currentMax = getMaxVolume(sampleBuffer);
                    maxVolumePerFrame.Add(currentMax);

                    //If the current frame is less than the max volume for the whole track, increase it
                    if (maxVolume < currentMax)
                    {
                        maxVolume = currentMax;
                    }
                }

                //Normalize the maxVolumePerFrame and create a has loud audio list
                foreach (var maxVol in maxVolumePerFrame)
                {
                    hasLoudAudio.Add(maxVol / maxVolume >= options.silent_threshold);
                }

                var audioFrameCount = hasLoudAudio.Count;

                //Build Chunks
                List<bool> shouldIncludeFrame = new List<bool>();

                List<Tuple<int, int, bool>> chunks = new List<Tuple<int, int, bool>>() { Tuple.Create(0, 0, false) };


                for (var i = 0; i < audioFrameCount; i++)
                {
                    var startIndex = (i - options.frame_margin);
                    var count = 1 + options.frame_margin;

                    if (startIndex + count > audioFrameCount - 1) count = (audioFrameCount - startIndex) - 1;
                    if (startIndex < 0) startIndex = 0;

                    shouldIncludeFrame.Add(hasLoudAudio.GetRange(startIndex, count).Any(p => p == true));

                    if (i >= 1 && shouldIncludeFrame[i] != shouldIncludeFrame[i - 1])
                    { // Did we flip?
                        chunks.Add(Tuple.Create(chunks.Last().Item2, i, shouldIncludeFrame[i - 1]));
                    }

                }

                chunks.Add(Tuple.Create(chunks.Last().Item2, audioFrameCount, shouldIncludeFrame[(audioFrameCount - 1) - 1]));
                chunks.RemoveAt(0);


                //Reset the stream 
                audioFileReader.Position = 0;

                //Process Chunks

                using (var writer = new WaveFileWriter(options.temp_dir + @"\finalAudio.wav", audioFileReader.WaveFormat))
                {

                    foreach (var chunk in chunks)
                    {

                        VarispeedSampleProvider sampleProv;

                        using (sampleProv = new VarispeedSampleProvider(audioFileReader, new SoundTouchProfile(true, false)))
                        {
                            sampleProv.PlaybackRate = (float)(chunk.Item3 ? options.sounded_speed : options.silent_speed );


                            var chunkLength = ((chunk.Item2 * samplesPerFrame) - (chunk.Item1 * samplesPerFrame));

                            int bufferSize = 10000;
                            float[] buffer = new float[bufferSize];
                            int samplesRead;
                            int totalSamplesRead = 0;

                            while ((samplesRead = sampleProv.Read(buffer, 0, bufferSize)) > 0)
                            {
                                writer.WriteSamples(buffer, 0, bufferSize);
                                totalSamplesRead += samplesRead;


                                //If we have reached the end, exit
                                if (totalSamplesRead >= chunkLength)
                                {
                                    break;
                                }

                                //If the next chunk we take is more than the chunk length, set the next buffer to fill the chunklength
                                if ((totalSamplesRead + bufferSize) > chunkLength)
                                {
                                    bufferSize = chunkLength - totalSamplesRead;
                                }

                            }



                        }
                    }
                }
            }


            HashSet<int> framesToDrop = new HashSet<int>();
            var random = new Random();

            //Drop 500% of random frames
            for (var i = 0; i < options.frame_count * 5; i += 1)
            {
                framesToDrop.Add(random.Next(1, options.frame_count));
            }

            return new List<int>(framesToDrop);


        }





        private float getMaxVolume(float[] s)
        {
            var maxv = s.Max();
            var minv = -s.Min();
            return new float[] { maxv, minv }.Max();
        }


        private void prepareAudio()
        {

            ///TODO write progress
            //Get the audio file from the video
            /////DEBUG 
            ///
            if (!File.Exists(tempAudio))
            {
                Console.WriteLine("Extracting audio...");
                var conv = Conversion.ExtractAudio(videoInputFile.FullName, tempAudio);
                conv.Start().Wait();
                Console.WriteLine("Audio Extracted.");
            }
            else
            {
                Console.WriteLine("DEBUG.... SKIPPING CONVERSION!!!");
            }
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
            }

            //If the output file already exists, delete it, if debug mode
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

            //Clear the temp dir
            ////TODO DEBUG
            if (tempdir.Exists)
            {
                //     tempdir.Delete(true);
            }

            //tempdir.Create();
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