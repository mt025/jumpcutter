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
            var chunkHasLoudAudio = new List<bool>();

            using (var wavFileReader = new WaveFileReader(tempAudio))
            {
                //Total audio samples (USING ONLY ONE CHANNEL)
                var audioSampleCount = wavFileReader.SampleCount;

                //Sample rate of the audio file
                var sampleRate = wavFileReader.WaveFormat.SampleRate;
                
                //Samples of audio per frame
                var samplesPerFrame = (int)(sampleRate / (double)options.frame_rate);

                //This is pretty much the same as the total video frame count
                //var audioFrameCount = (int)Math.Ceiling(new decimal(audioSampleCount / samplesPerFrame))+1;


                float maxVolume = 0;

                float[] frame;

                while ((frame = ReadNextSampleFrames(wavFileReader,100000)) != null)
                {
                
                    var currentMax = getMaxVolume(frame);
                    if (maxVolume < currentMax)
                    {
                        maxVolume = currentMax;
                    }
                
                }

                wavFileReader.Position = 0;

                List<bool> hasLoudAudio = new List<bool>();


                //Loop each frame and grab all samples
                while ((frame = ReadNextSampleFrames(wavFileReader, samplesPerFrame)) != null)
                {
                    var currentMax = getMaxVolume(frame)/maxVolume;
                    hasLoudAudio.Add(currentMax >= options.silent_threshold);                   
                }

                var audioFrameCount = hasLoudAudio.Count;


                Console.WriteLine(hasLoudAudio);


                // using (var readerStream = WaveFormatConversionStream.CreatePcmStream(wavFileReader))
                // {
                //     using (var blockStream = new BlockAlignReductionStream(readerStream))
                //     {
                //
                //         var sourceBytesPerSample = (blockStream.WaveFormat.BitsPerSample / 8) * blockStream.WaveFormat.Channels;
                //         var sampleChannel = new SampleChannel(blockStream, false);
                //         var destBytesPerSample = 4 * sampleChannel.WaveFormat.Channels;
                //
                //         //var  length = destBytesPerSample * (readerStream.Length / sourceBytesPerSample);
                //
                //         for (var i = 0; i <= audioFrameCount; i++)
                //         {
                //
                //             float[] buffer = new float[samplesPerFrame];
                //             sampleChannel.Read(buffer, 0, samplesPerFrame);
                //             for (var b = 0; b <= buffer.Length; b++)
                //             {
                //                 var currentMax = getMaxVolume(buffer);
                //                 if (maxVolume < currentMax)
                //                 {
                //                     maxVolume = currentMax;
                //                 }
                //
                //             }
                //         }
                //     }

                Console.WriteLine(maxVolume);

            }
            HashSet<int> framesToDrop = new HashSet<int>();
            var random = new Random();

            //Drop 500% of random frames
            for (var i = 0; i < options.frame_count * 5; i += 1)
            {
                //framesToDrop.Add(random.Next(1, options.frame_count));
            }

            return new List<int>(framesToDrop);
        }



        //Edit of NAudio.Wave.WaveFileReader.ReadNextSampleFrame to allow retrevial of multiple samples in a batch
        public float[] ReadNextSampleFrames(WaveFileReader wfr,int count = 1)
        {
            switch (wfr.WaveFormat.Encoding)
            {
                case WaveFormatEncoding.Pcm:
                case WaveFormatEncoding.IeeeFloat:
                case WaveFormatEncoding.Extensible: // n.b. not necessarily PCM, should probably write more code to handle this case
                    break;
                default:
                    throw new InvalidOperationException("Only 16, 24 or 32 bit PCM or IEEE float audio data supported");
            }
            var sampleFrame = new float[wfr.WaveFormat.Channels * count];
            int bytesPerSample = wfr.WaveFormat.Channels * (wfr.WaveFormat.BitsPerSample / 8);
            int bytesToRead = bytesPerSample * count;
            byte[] raw = new byte[bytesToRead];
            int bytesRead = wfr.Read(raw, 0, bytesToRead);
            if (bytesRead == 0) return null; // end of file
            if (bytesRead < bytesToRead)
            {
                count = bytesRead / bytesPerSample;
            }
            int offset = 0;
            for (int index = 0; index < wfr.WaveFormat.Channels*count; index++)
            {
                if (wfr.WaveFormat.BitsPerSample == 16)
                {
                    sampleFrame[index] = BitConverter.ToInt16(raw, offset) / 32768f;
                    offset += 2;
                }
                else if (wfr.WaveFormat.BitsPerSample == 24)
                {
                    sampleFrame[index] = (((sbyte)raw[offset + 2] << 16) | (raw[offset + 1] << 8) | raw[offset]) / 8388608f;
                    offset += 3;
                }
                else if (wfr.WaveFormat.BitsPerSample == 32 && wfr.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat)
                {
                    sampleFrame[index] = BitConverter.ToSingle(raw, offset);
                    offset += 4;
                }
                else if (wfr.WaveFormat.BitsPerSample == 32)
                {
                    sampleFrame[index] = BitConverter.ToInt32(raw, offset) / (Int32.MaxValue + 1f);
                    offset += 4;
                }
                else
                {
                    throw new InvalidOperationException("Unsupported bit depth");
                }
            }
            return sampleFrame;
        }


        private float getMaxVolume(float[] s)
        {
            var maxv = s.Max();
            var minv = -s.Min();
            return new float[] { maxv, minv }.Max();
        }


        public static float[] ReadInAllSamples(string file)
        {
            ISampleProvider reader = new AudioFileReader(file);

            List<float> allSamples = new List<float>();
            float[] samples = new float[16384];

            while (reader.Read(samples, 0, samples.Length) > 0)
            {
                for (int i = 0; i < samples.Length; i++)
                    allSamples.Add(samples[i]);
            }

            samples = new float[allSamples.Count];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = allSamples[i];

            return samples;
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