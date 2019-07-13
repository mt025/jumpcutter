using NAudio.Wave;
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

        private List<int> videoFramesToRender = new List<int>();

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


        internal List<int> writeAudio()
        {
            var chunks = buildChunks();

            using (var audioFileWriter = new WaveFileWriter(options.temp_dir + @"\finalAudio.wav", audioFileReader.WaveFormat))
            {
                var excess = 0.0;
                var normalSpeed = true;
                foreach (var chunk in chunks)
                {
                    var chunkSpeed = chunk.getSpeed(options);
                    normalSpeed = chunkSpeed == 1;
                    var totalSamplesRead = vocodeAudio(chunk, audioFileReader, audioFileWriter);
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

            return videoFramesToRender;
        }

        private List<Chunk> buildChunks()
        {

            var chunks = new List<Chunk>();

            var bufferCount = samplesPerFrame;
            float[] sampleBuffer = new float[bufferCount];
            int samplesRead;
            var currentFrame = 0;
            bool? lastLoudFrame = null;
            Queue<float[]> lastBuffers = new Queue<float[]>();

            while ((samplesRead = audioFileReader.Read(sampleBuffer, 0, bufferCount)) > 0)
            {
                //Report output
                utils.reportStatus(READ_VOLUME_MESSAGE, currentFrame, options.frame_count, 0);

                //If its not the whole buffer returned, take the remaining amount
                if (samplesRead != bufferCount) sampleBuffer = sampleBuffer.Take(samplesRead).ToArray();

                //Get max volume of current frame
                var currentMax = getMaxVolume(sampleBuffer);

                //If the current frame is less than the max volume for the whole track, increase it
                if (maxVolume < currentMax) maxVolume = currentMax;

                //Is the frame loud?
                var loudFrame = currentMax / maxVolume >= options.silent_threshold;

                //Has the loudness changed?
                if (lastLoudFrame != null && lastLoudFrame != loudFrame)
                {

                    int takeFramesUntil;

                    //If the next chunk is going to be loud offset, current quiet frame by - frame_margin
                    if (loudFrame && currentFrame != 0)
                    {
                        takeFramesUntil = currentFrame - options.frame_margin;
                    }
                    //If the next chunk is going to be quiet, offset loud by + frame_margin
                    else
                    {
                        takeFramesUntil = currentFrame + options.frame_margin;
                        if (takeFramesUntil > options.frame_count) { takeFramesUntil = options.frame_count; }
                    }


                    //If framesToTake is larger than the current frame count set it to the frame count
                    if (takeFramesUntil > options.frame_count) takeFramesUntil = options.frame_count;

                    //Add this chunk
                    var lastEnd = chunks.Any() ? chunks.Last().endFrame : 0;

                    var difference = takeFramesUntil - lastEnd;

                    //Unless its less frames than 0
                    if (difference > 0)
                    {
                        chunks.Add(new Chunk(lastEnd, takeFramesUntil, (bool)lastLoudFrame));
                    }

                }

                lastLoudFrame = loudFrame;
                currentFrame++;

                ////Not sure if we are going to need the previous frame_margin buffers yet, we will see.... remove if not used in future
                if (lastBuffers.Count > options.frame_margin) lastBuffers.Dequeue();
                lastBuffers.Enqueue(sampleBuffer);
            }

            if (options.frame_count - chunks.Last().endFrame > 0)
            {
                chunks.Add(new Chunk(chunks.Last().endFrame, options.frame_count, (bool)lastLoudFrame));
            }

            utils.reportStatus(READ_VOLUME_MESSAGE, options.frame_count, options.frame_count, last: true);
            Console.WriteLine("\tMax Audio Volume is " + maxVolume + "/1");
            calculateTimeReduction(chunks);

            //Reset the stream 
            audioFileReader.Position = 0;

            return chunks;
        }

        private void calculateTimeReduction(List<Chunk> chunks)
        {
            double totalDuration = 0;
            //Caulucate duration difference 
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
            SoundTouch.SoundTouch<float, double> soundTouch = null;
            var speed = (chunk.getSpeed(options) - 1) * 100;
            var normalSpeed = speed == 0;
            var samplesLeft = false;

            if (!normalSpeed)
            {
                //Init soundtouch
                soundTouch = new SoundTouch.SoundTouch<float, double>();

                soundTouch.SetRate(1.0f);
                soundTouch.SetPitchOctaves(0f);
                soundTouch.SetTempo(0);

                soundTouch.SetTempoChange(speed);
                soundTouch.SetSampleRate((int)(audioFileReader.WaveFormat.SampleRate));
                soundTouch.SetChannels(audioFileReader.WaveFormat.Channels);

            }

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

            while (!readAll || samplesLeft)
            {
                if (!readAll)
                {
                    utils.reportStatus(WRITE_AUDIO_MESSAGE, chunk.startFrame + (totalSamplesRead / samplesPerFrame), options.frame_count, 2, soundTouch == null ? "STD":"VOC");

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
                        if (!normalSpeed) soundTouch.Flush();
                    }
                    else
                    {
                        if (soundTouch != null)
                        {
                            //Read the next block and and input them into soundtouch
                            nSamples = samplesRead / audioFileReader.WaveFormat.Channels;
                            soundTouch.PutSamples(inputBuffer, nSamples);
                        }
                    }
                }

                do
                {
                    if (soundTouch != null)
                    {
                        //Get samples from soundtouch
                        nSamples = soundTouch.ReceiveSamples(outputBuffer, outputBuffer.Length / audioFileReader.WaveFormat.Channels);

                        //Write samples to file
                        audioFileWriter.WriteSamples(outputBuffer, 0, nSamples * audioFileReader.WaveFormat.Channels);
                    }
                    else
                    {
                        audioFileWriter.WriteSamples(inputBuffer, 0, bufferSize);
                        nSamples = 0;
                    }

                } while (nSamples != 0);


                totalSamplesRead += samplesRead;
                samplesLeft = soundTouch?.AvailableSamples > 0;
            }

            soundTouch?.Clear();

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
