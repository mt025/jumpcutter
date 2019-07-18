using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xabe.FFmpeg;

namespace Jumpcutter_dot_net
{
    public class AudioProcessor2
    {
        private Options options;
        private readonly Utils utils;


        private AudioFileReader audioFileReader;
        private int sampleRate;

        public JumpCutterStreamProcessor stream;

        public AudioProcessor2(ref Options options)
        {
            this.options = options;
            this.utils = new Utils();
            options.temp_audio = options.temp_dir + @"\" + "fullaudio.wav";
        }
        internal void PrepareAudio()
        {

            ///TODO write progress
            //Get the audio file from the video
            /////DEBUG 
            ///
            if (!File.Exists(options.temp_audio))
            {
                var conv = Conversion.ExtractAudio(options.input_file, options.temp_audio);
                conv.Start().Wait();
            }
            else
            {
                //Console.WriteLine("DEBUG.... SKIPPING CONVERSION!!!");
            }

            audioFileReader = new AudioFileReader(options.temp_audio);


            //Sample rate of the audio file
            sampleRate = audioFileReader.WaveFormat.SampleRate;

            //Samples of audio per frame
            var samplesPerFrame = (int)((sampleRate * audioFileReader.WaveFormat.Channels) / (double)options.frame_rate);

            //Align samples per frame by the block
            //options.samplesPerFrame = reAlignBuffer(samplesPerFrame, audioFileReader.WaveFormat.BlockAlign);
            options.samplesPerFrame = samplesPerFrame;


            Console.WriteLine("\tAudio Extracted");
        }


        public List<int> writeAudio()
        {
            //This vocodes the stream
            var stream = new JumpCutterStreamProcessor(audioFileReader, ref options);
            var frameAudio = new byte[options.samplesPerFrame * 4];
            int readCount;
            var audioFileWriter = new WaveFileWriter(options.temp_dir + @"\finalAudio.wav", audioFileReader.WaveFormat);

            while ((readCount = stream.Read(frameAudio, 0, frameAudio.Length)) > 0)
            {
                audioFileWriter.Write(frameAudio, 0, readCount);

            }

            audioFileWriter.Dispose();

            return stream.getFrames();
        }

        public void prepareStream()
        {
            //This makes the stream realtime
            WaveChannel32 inputStream = new WaveChannel32(audioFileReader)
            {
                PadWithZeroes = false  // don't pad, otherwise the stream never ends
            };
            //This vocodes the stream
            stream = new JumpCutterStreamProcessor(inputStream, ref options);

        }

        public void Stream()
        {

            var waveOut = new WaveOut()
            {
                DesiredLatency = 100
            };

            waveOut.Init(stream);
            waveOut.Play();
        }


        private int reAlignBuffer(int bufferSize, int blocksize)
        {
            var rem = bufferSize % blocksize;
            return rem == 0 ? bufferSize : bufferSize - rem;
        }



    }
}
