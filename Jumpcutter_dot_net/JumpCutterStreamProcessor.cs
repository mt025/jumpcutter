//////////////////////////////////////////////////////////////////////////////
///
/// WaveStream processor class for manipulating audio stream in C# with 
/// SoundTouch library.
/// 
/// This module uses NAudio library for C# audio file input / output
/// 
/// Author        : Copyright (c) Olli Parviainen
/// Author e-mail : oparviai 'at' iki.fi
/// SoundTouch WWW: http://www.surina.net/soundtouch
///
////////////////////////////////////////////////////////////////////////////////
//
// License for this source code file: Microsoft Public License(Ms-PL)
//
////////////////////////////////////////////////////////////////////////////////

using NAudio.Wave;
using SoundTouch;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jumpcutter_dot_net
{
    /// <summary>
    /// NAudio WaveStream class for processing audio stream with SoundTouch effects
    /// </summary>
    public class JumpCutterStreamProcessor : WaveStream
    {
        private readonly WaveChannel32 inputWC32;
        private readonly AudioFileReader inputAFR;

        public SoundTouch<float,double> st;

        private float[] floatbuffer;
        private byte[] bytebuffer;

        private Options options;

        private List<int> framesToRender;

        private int currentFrameCount = 0;


        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="inputWC32">WaveChannel32 stream used for processor stream input</param>
        public JumpCutterStreamProcessor(WaveChannel32 inputWC32,ref Options options)
        {
            this.inputWC32 = inputWC32;
            st = new SoundTouch<float,double>();
            st.SetChannels(inputWC32.WaveFormat.Channels);
            st.SetSampleRate(inputWC32.WaveFormat.SampleRate);
            bytebuffer = new byte[options.samplesPerFrame*4];
            floatbuffer = new float[options.samplesPerFrame];
            this.options = options;
    }

        public JumpCutterStreamProcessor(AudioFileReader inputAFR, ref Options options)
        {
            this.inputAFR = inputAFR;
            st = new SoundTouch<float, double>();
            st.SetChannels(inputAFR.WaveFormat.Channels);
            st.SetSampleRate(inputAFR.WaveFormat.SampleRate);
            bytebuffer = new byte[options.samplesPerFrame * 4];
            floatbuffer = new float[options.samplesPerFrame];
            this.options = options;
            framesToRender = new List<int>();
        }

        private void shouldRenderFrame() {

        }
        public delegate void AudioFrameHandler();
        public event AudioFrameHandler AudioFrameRendered;

        protected virtual void OnAudioFrameRendered()
        {
            AudioFrameRendered?.Invoke();
        }

        internal List<int> getFrames() => framesToRender;

        /// <summary>
        /// True if end of stream reached
        /// </summary>
        public bool EndReached { get; private set; } = false;


        public override long Length
        {
            get
            {
                return inputWC32.Length;
            }
        }


        public override long Position
        {
            get
            {
                return inputWC32.Position;
            }

            set
            {
                inputWC32.Position = value;
            }
        }


        public override WaveFormat WaveFormat
        {
            get
            {
                return inputWC32.WaveFormat;
            }
        }

        private bool normal = true;


        /// <summary>
        /// Overridden Read function that returns samples processed with SoundTouch. Returns data in same format as
        /// WaveChannel32 i.e. stereo float samples.
        /// </summary>
        /// <param name="buffer">Buffer where to return sample data</param>
        /// <param name="offset">Offset from beginning of the buffer</param>
        /// <param name="count">Number of bytes to return</param>
        /// <returns>Number of bytes copied to buffer</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            var normalb4 = normal;

            currentFrameCount++;
            try
            {
                // Iterate until enough samples available for output:
                // - read samples from input stream
                // - put samples to SoundStretch processor
                while (st.AvailableSamples < count)
                {
                    int nbytes;
                    if (inputWC32 != null)
                    {
                        nbytes = inputWC32.Read(bytebuffer, 0, bytebuffer.Length);
                    }
                    else
                    {
                        nbytes = inputAFR.Read(bytebuffer, 0, bytebuffer.Length);
                    }


                    if (nbytes == 0)
                    {
                        // end of stream. flush final samples from SoundTouch buffers to output
                        if (EndReached == false)
                        {
                            EndReached = true;  // do only once to avoid continuous flushing
                            st.Flush();
                        }
                        break;
                    }

                    // binary copy data from "byte[]" to "float[]" buffer
                    Buffer.BlockCopy(bytebuffer, 0, floatbuffer, 0, nbytes);
                    var max = GetMaxVolume(floatbuffer);
        
                    if (max >= options.silent_threshold)
                    {
                        normal = true;
                        st.SetTempoChange((options.sounded_speed - 1) * 100);
                    }
                    else
                    {
                        normal = false;
                        st.SetTempoChange((options.silent_speed - 1) * 100);
                    }

                    st.PutSamples(floatbuffer, (nbytes / 8));
                }

                // ensure that buffer is large enough to receive desired amount of data out
                if (floatbuffer.Length < count / 4)
                {
                    floatbuffer = new float[count / 4];
                }
                // get processed output samples from SoundTouch
                int numsamples = st.ReceiveSamples(floatbuffer, (count / 8));
                // binary copy data from "float[]" to "byte[]" buffer
                Buffer.BlockCopy(floatbuffer, 0, buffer, offset, numsamples * 8);

                if (normal != normalb4) {
                    OnAudioFrameRendered();
                }


                return numsamples * 8;  // number of bytes
            }
            catch (Exception) { 

                return 0;
            }
        }

        /// <summary>
        /// Clear the internal processor buffers. Call this if seeking or rewinding to new position within the stream.
        /// </summary>
        public void Clear()
        {
            st.Clear();
            EndReached = false;
        }

        private float GetMaxVolume(float[] s)
        {
            var maxv = s.Max();
            var minv = -s.Min();
            return new float[] { maxv, minv }.Max();
        }

    }
}
