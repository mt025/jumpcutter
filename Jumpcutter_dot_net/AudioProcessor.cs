﻿using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xabe.FFmpeg;

namespace Jumpcutter_dot_net
{
    internal class AudioProcessor
    {
        private Options options;
        private Utils utils;
        private string tempAudio;

        private AudioFileReader audioFileReader;
        private int sampleRate;
        private int samplesPerFrame;
        private float maxVolume;


        const string READ_VOLUME_MESSAGE = "\tReading frame volume {0} out of {1} {2}";
        const string WRITE_AUDIO_MESSAGE = "\tWriting new audio ({0} mode)  {1} out of {2} {3}";


        public AudioProcessor(Options options)
        {
            this.options = options;
            this.utils = new Utils();
            tempAudio = options.temp_dir + @"\" + "fullaudio.wav";
        }

        internal void prepareAudio()
        {

            ///TODO write progress
            //Get the audio file from the video
            /////DEBUG 
            ///
            if (!File.Exists(tempAudio))
            {
                var conv = Conversion.ExtractAudio(options.input_file, tempAudio);
                conv.Start().Wait();
            }
            else
            {
                //Console.WriteLine("DEBUG.... SKIPPING CONVERSION!!!");
            }

            audioFileReader = new AudioFileReader(tempAudio);


            //Sample rate of the audio file
            sampleRate = audioFileReader.WaveFormat.SampleRate;

            //Samples of audio per frame
            samplesPerFrame = (int)((sampleRate * audioFileReader.WaveFormat.Channels) / (double)options.frame_rate);

            //var samplePerFrameBuffer = samplesPerFrame - (samplesPerFrame % 4);

            Console.WriteLine("\tAudio Extracted");
        }


        internal List<int> processAudio()
        {

            List<int> videoFramesToRender = new List<int>();
            List<bool> hasLoudAudioPerFrame = processVolumes();


            trimExtraAudio(hasLoudAudioPerFrame);
            var chunks = buildChunks(hasLoudAudioPerFrame);
            calculateTimeReduction(chunks);

            using (var audioFileWriter = new WaveFileWriter(options.temp_dir + @"\finalAudio.wav", audioFileReader.WaveFormat))
            {
                var excess = 0.0;
                var normalSpeed = true;
                foreach (var chunk in chunks)
                {
                    var chunkSpeed = chunk.getSpeed(options);
                    normalSpeed = chunkSpeed == 1;
                    var totalSamplesRead = (normalSpeed ?  standardAudio(chunk, audioFileReader, audioFileWriter) : vocodeAudio(chunk, audioFileReader, audioFileWriter));
                    //Build render list
                    var frameCount = (chunk.endFrame - chunk.startFrame);
                    //Count frames to render
                    var framesToRender = frameCount / chunkSpeed;
                    //Save non whole frames
                    excess += (framesToRender % 1);

                    framesToRender = (int)framesToRender;
                    //If we have a spare frame, use it
                    if (excess >= 1)
                    {
                        framesToRender++;
                        excess -= 1;
                    }


                    for (var i = 0; i < framesToRender; i++)
                    {
                        //Which frame should render?
                        var frameToRender = (int)Math.Round(utils.Remap(i, 0, framesToRender, chunk.startFrame, chunk.endFrame), MidpointRounding.AwayFromZero);

                        videoFramesToRender.Add(frameToRender);

                    }
                }
                utils.reportStatus(WRITE_AUDIO_MESSAGE, options.frame_count, options.frame_count, 2, normalSpeed ? "STD" : "VOC", true);
            }

            audioFileReader.Dispose();








            return new List<int>(videoFramesToRender);
        }



        private List<bool> processVolumes()
        {
            List<bool> hasLoudAudioPerFrame = new List<bool>();

            var bufferCount = samplesPerFrame;
            //Get Max Volume for each frame
            float[] sampleBuffer = new float[bufferCount];
            int samplesRead;

            var loopCount = 0;
            while ((samplesRead = audioFileReader.Read(sampleBuffer, 0, bufferCount)) > 0)
            {
                loopCount++;

                utils.reportStatus(READ_VOLUME_MESSAGE, loopCount, options.frame_count, 0);

                //If its not the whole buffer, take the remaining amount
                if (samplesRead != bufferCount)
                {
                    sampleBuffer = sampleBuffer.Take(samplesRead).ToArray();
                }

                var currentMax = getMaxVolume(sampleBuffer);
                hasLoudAudioPerFrame.Add(currentMax / maxVolume >= options.silent_threshold);

                //If the current frame is less than the max volume for the whole track, increase it
                if (maxVolume < currentMax)
                {
                    maxVolume = currentMax;
                }

            }


            utils.reportStatus(READ_VOLUME_MESSAGE, options.frame_count, options.frame_count, last: true);

            Console.WriteLine("\tMax Audio Volume is " + maxVolume + "/1");

            //Reset the stream 
            audioFileReader.Position = 0;


            return hasLoudAudioPerFrame;
        }
         
        private List<Chunk> buildChunks(List<bool> hasLoudAudio)
        {
            var chunks = new List<Chunk>();

            List<bool> audioLevelHigh = new List<bool>();

            chunks.Add(new Chunk(0, 0, false));


            for (var i = 0; i < options.frame_count; i++)
            {
                var startIndex = (i - options.frame_margin);
                var count = 1 + options.frame_margin;

                if (startIndex + count > options.frame_count - 1) count = (options.frame_count - startIndex) - 1;
                if (startIndex < 0) startIndex = 0;

                audioLevelHigh.Add(hasLoudAudio.GetRange(startIndex, count).Any(p => p == true));

                if (i >= 1 && audioLevelHigh[i] != audioLevelHigh[i - 1])
                {
                    //If the audio level has changed, start a new chunk
                    chunks.Add(new Chunk(chunks.Last().endFrame, i, audioLevelHigh[i - 1]));
                }

            }
            //Add the last block
            chunks.Add(new Chunk(chunks.Last().endFrame, options.frame_count, audioLevelHigh[(options.frame_count - 1) - 1]));
            chunks.RemoveAt(0);


            return chunks;

        }

        private void calculateTimeReduction(List<Chunk> chunks)
        {
            double totalDuration = 0;
            /////Caulucate duration difference 
            foreach (var chunk in chunks)
            {
                var chunkLength = chunk.endFrame - chunk.startFrame;
                var playbackRate = chunk.getSpeed(options);
                var duration = chunkLength / playbackRate;
                totalDuration += duration;
            }

            var durationChange = 100 - (int)((((totalDuration / options.frame_count)) * 100));
            Console.WriteLine("\tNew video length will have a " + durationChange + "% reduction (" + options.frame_count + "s to " + (int)totalDuration + "s)");
        }

        private int vocodeAudio(Chunk chunk, AudioFileReader audioFileReader, WaveFileWriter audioFileWriter)
        {
            ///TODO Create the soundtouch outside this loop, and just reset it!!!!!!

            //Init soundtouch
            SoundTouch.SoundTouch<float, double> soundTouch = new SoundTouch.SoundTouch<float, double>();

            soundTouch.SetRate(1.0f);
            soundTouch.SetPitchOctaves(0f);
            soundTouch.SetTempo(0);

            soundTouch.SetTempoChange((chunk.getSpeed(options) - 1) * 100);
            soundTouch.SetSampleRate((int)(audioFileReader.WaveFormat.SampleRate));
            soundTouch.SetChannels(audioFileReader.WaveFormat.Channels);

            //Sample count of chunk
            var chunkSamplesLength = ((chunk.endFrame * samplesPerFrame) - (chunk.startFrame * samplesPerFrame));


            int totalSamplesRead = 0;

            //Set buffer size to 6750 or chunkSamplesLength if its less
            int bufferSize = chunkSamplesLength < 6750 ? chunkSamplesLength : 6750;

            //Init buffers
            float[] inputBuffer = new float[bufferSize];
            float[] outputBuffer = new float[bufferSize];

            var samplesRead = 0;


            var readAll = false;
            var nSamples = 0;

            while (!readAll || soundTouch.AvailableSamples > 0)
            {
                if (!readAll)
                {
                    utils.reportStatus(WRITE_AUDIO_MESSAGE, chunk.startFrame + (totalSamplesRead / samplesPerFrame), options.frame_count, 2,"VOC");

                    //Shorten buffer if needed
                    if (bufferSize > chunkSamplesLength - totalSamplesRead)
                    {
                        bufferSize = chunkSamplesLength - totalSamplesRead;
                    }


                    //Read next block of samples
                    samplesRead = audioFileReader.Read(inputBuffer, 0, bufferSize);

                    //End of stream or chunk finished
                    if (samplesRead == 0 || totalSamplesRead >= chunkSamplesLength)
                    {
                        //Stop reading more and flush the soundtouch buffer
                        readAll = true;
                        soundTouch.Flush();
                    }
                    else
                    {
                        //Read the next block and and input them into soundtouch
                        nSamples = samplesRead / audioFileReader.WaveFormat.Channels;
                        soundTouch.PutSamples(inputBuffer, nSamples);
                    }
                }

                do
                {
                    //Get samples from soundtouch
                    nSamples = soundTouch.ReceiveSamples(outputBuffer, outputBuffer.Length / audioFileReader.WaveFormat.Channels);

                    //Write samples to file
                    audioFileWriter.WriteSamples(outputBuffer, 0, nSamples * audioFileReader.WaveFormat.Channels);

                } while (nSamples != 0);


                totalSamplesRead += samplesRead;

            }


            soundTouch.Clear();
            return totalSamplesRead;
        }


        private int standardAudio(Chunk chunk, AudioFileReader audioFileReader, WaveFileWriter audioFileWriter)
        {
            //Sample count of chunk
            var chunkSamplesLength = ((chunk.endFrame * samplesPerFrame) - (chunk.startFrame * samplesPerFrame));

            int totalSamplesRead = 0;

            //Set buffer size to 6750 or chunkSamplesLength if its less
            int bufferSize = chunkSamplesLength < 6750 ? chunkSamplesLength : 6750;

            //Init buffers
            float[] inputBuffer = new float[bufferSize];
            float[] outputBuffer = new float[bufferSize];
            var readAll = false;

            while (!readAll)
            {

                utils.reportStatus(WRITE_AUDIO_MESSAGE, chunk.startFrame + (totalSamplesRead / samplesPerFrame), options.frame_count, 2,"STD");


                //Shorten buffer if needed
                if (bufferSize > chunkSamplesLength - totalSamplesRead) {
                    bufferSize = chunkSamplesLength - totalSamplesRead;
                }

                //Read next block of samples
                int samplesRead = audioFileReader.Read(inputBuffer, 0, bufferSize);
                totalSamplesRead += samplesRead;
                //End of stream or chunk finished
                if (samplesRead == 0 || totalSamplesRead >= chunkSamplesLength)
                {
                    //Stop reading more and flush the soundtouch buffer
                    readAll = true;
                }
                else
                {
                    //Read the next block and and input them into soundtouch
                }
                audioFileWriter.WriteSamples(inputBuffer, 0, bufferSize);


            }

            return totalSamplesRead;
        }


        private void trimExtraAudio(List<bool> hasLoudAudioPerFrame)
        {
            var audioLengthDifference = hasLoudAudioPerFrame.Count - options.frame_count;

            //If the audio is longer than the video, trim the audio
            if (audioLengthDifference > 0)
            {
                Console.WriteLine("\tAudio is longer by " + audioLengthDifference + " frames. Trimming audio end.");
                //If the audio track is longer than video track, trim the front of the audio track (Does this always work)?
                hasLoudAudioPerFrame.RemoveRange(hasLoudAudioPerFrame.Count - 1 - audioLengthDifference, audioLengthDifference);
            }
            else if (audioLengthDifference < 0)
            {
                Console.WriteLine("\tAudio is shorter by " + -audioLengthDifference + " frames.");
            }
        }

        private float getMaxVolume(float[] s)
        {
            var maxv = s.Max();
            var minv = -s.Min();
            return new float[] { maxv, minv }.Max();
        }

    }
}