﻿using System.IO;
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

                while ((frame = Varispeed.SampleFrameReader.ReadNextSampleFrames(wavFileReader, 100000)) != null)
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
                while ((frame = Varispeed.SampleFrameReader.ReadNextSampleFrames(wavFileReader, samplesPerFrame)) != null)
                {
                    var currentMax = getMaxVolume(frame) / maxVolume;
                    hasLoudAudio.Add(currentMax >= options.silent_threshold);
                }

                var audioFrameCount = hasLoudAudio.Count;


                Console.WriteLine(hasLoudAudio);

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

                Console.WriteLine(chunks);

                wavFileReader.Position = 0;





                foreach (var chunk in chunks)
                {

                    var chunkLength = (chunk.Item2 * samplesPerFrame) - (chunk.Item1 * samplesPerFrame);
                    var sampProv = new SampleChannel(wavFileReader,false);
                    VarispeedSampleProvider sampleProv;

                    using (sampleProv = new VarispeedSampleProvider(sampProv, new SoundTouchProfile(true, false)))
                    {
                        using (var writer = new WaveFileWriter(options.temp_dir + @"\" + (chunk.Item3 ? "Loud" : "Quiet") + "_chunk_" + chunk.Item1 + " .wav", wavFileReader.WaveFormat))
                        {
                            int bufferSize = 10000;
                            float[] buffer = new float[bufferSize];
                            int samplesRead;
                            int totalSamplesRead = 0;

                            

                            while ((samplesRead = sampleProv.Read(buffer, 0, bufferSize)) > 0)
                            {

                                writer.WriteSamples(buffer,0, bufferSize);
                                totalSamplesRead += samplesRead;

                                //If the next chunk we take is more than the chunk length, set the next buffer to fill the chunklength
                                if ((totalSamplesRead + bufferSize) > chunkLength)
                                {
                                    bufferSize = chunkLength - totalSamplesRead;
                                }

                                //If we have reached the end, exit
                                if (totalSamplesRead >= chunkLength)
                                {
                                    break;
                                }



                            }

                        }


                    }
                        



                    // var audioData = ReadNextSampleFrames(wavFileReader, chunkLength);
                    //
                    // var soundTouchProfile = new SoundTouchProfile(true, false)
                    // {
                    //     Channels = wavFileReader.WaveFormat.Channels,
                    //     SampleRate = wavFileReader.WaveFormat.SampleRate,
                    // };
                    //
                    //
                    // var soundTouch = new SoundTouchProcessor(audioData, soundTouchProfile);

 
                   



                    //var speedControl = new VarispeedSampleProvider(, 100, new SoundTouchProfile(true, false));









                    //      outputAudioData = np.zeros((0, audioData.shape[1]))
                    //      outputPointer = 0
                    //      
                    //      lastExistingFrame = None
                    //      for chunk in chunks:
                    //          audioChunk = audioData[int(chunk[0] * samplesPerFrame):int(chunk[1] * samplesPerFrame)]
                    //      
                    //      
                    //          sFile = TEMP_FOLDER + "/tempStart.wav"
                    //          eFile = TEMP_FOLDER + "/tempEnd.wav"
                    //          wavfile.write(sFile, SAMPLE_RATE, audioChunk)
                    //          with WavReader(sFile) as reader:
                    //              with WavWriter(eFile, reader.channels, reader.samplerate) as writer:
                    //                  tsm = phasevocoder(reader.channels, speed = NEW_SPEED[int(chunk[2])])
                    //                  tsm.run(reader, writer)
                    //          _, alteredAudioData = wavfile.read(eFile)
                    //          leng = alteredAudioData.shape[0]
                    //          endPointer = outputPointer + leng
                    //          outputAudioData = np.concatenate((outputAudioData, alteredAudioData / maxAudioVolume))
                    //      
                    //          # outputAudioData[outputPointer:endPointer] = alteredAudioData/maxAudioVolume
                    //      
                    //      # smooth out transitiion's audio by quickly fading in/out
                    //      
                    //                          if leng < AUDIO_FADE_ENVELOPE_SIZE:
                    //              outputAudioData[outputPointer: endPointer] = 0 # audio is less than 0.01 sec, let's just remove it.
                    //          else:
                    //              premask = np.arange(AUDIO_FADE_ENVELOPE_SIZE) / AUDIO_FADE_ENVELOPE_SIZE
                    //              mask = np.repeat(premask[:, np.newaxis], 2, axis = 1) # make the fade-envelope mask stereo
                    //              outputAudioData[outputPointer: outputPointer + AUDIO_FADE_ENVELOPE_SIZE] *= mask
                    //              outputAudioData[endPointer - AUDIO_FADE_ENVELOPE_SIZE:endPointer] *= 1 - mask
                    //      
                    //          startOutputFrame = int(math.ceil(outputPointer / samplesPerFrame))
                    //          endOutputFrame = int(math.ceil(endPointer / samplesPerFrame))
                    //          for outputFrame in range(startOutputFrame, endOutputFrame):
                    //              inputFrame = int(chunk[0] + NEW_SPEED[int(chunk[2])] * (outputFrame - startOutputFrame))
                    //              didItWork = copyFrame(inputFrame, outputFrame)
                    //              if didItWork:
                    //                  lastExistingFrame = inputFrame
                    //              else:
                    //                  copyFrame(lastExistingFrame, outputFrame)
                    //      
                    //          outputPointer = endPointer
                    //      
                    //      wavfile.write(TEMP_FOLDER + "/audioNew.wav", SAMPLE_RATE, outputAudioData)

                    // using (var readerStream = WaveFormatConversionStream.CreatePcmStream(wavFileReader))
                    // {
                    //     using (var blockStream = new BlockAlignReductionStream(readerStream))
                    //     {
                    //
                    //         var sourceBytesPerSample = (blockStream.WaveFormat.BitsPerSample / 8) * blockStream.WaveFormat.Channels;
                    //         var sampleChannel = new SampleChannel(blockStream, false);
                    //         var destBytesPerSample = 4 * sampleChannel.WaveFormat.Channels;y
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