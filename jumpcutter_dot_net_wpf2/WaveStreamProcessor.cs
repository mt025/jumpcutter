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

namespace jumpcutter_dot_net_wpf2
{
    /// <summary>
    /// NAudui WaveStream class for processing audio stream with SoundTouch effects
    /// </summary>
    public class WaveStreamProcessor : WaveStream
    {
        private readonly WaveChannel32 inputStr;
        public SoundTouch<float,double> st;

        private readonly byte[] bytebuffer = new byte[4096];
        private float[] floatbuffer = new float[1024];


        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="input">WaveChannel32 stream used for processor stream input</param>
        public WaveStreamProcessor(WaveChannel32 input)
        {
            inputStr = input;
            st = new SoundTouch<float,double>();
            st.SetChannels(input.WaveFormat.Channels);
            st.SetSampleRate(input.WaveFormat.SampleRate);
        }

        /// <summary>
        /// True if end of stream reached
        /// </summary>
        public bool EndReached { get; private set; } = false;


        public override long Length
        {
            get
            {
                return inputStr.Length;
            }
        }


        public override long Position
        {
            get
            {
                return inputStr.Position;
            }

            set
            {
                inputStr.Position = value;
            }
        }


        public override WaveFormat WaveFormat
        {
            get
            {
                return inputStr.WaveFormat;
            }
        }

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
            try
            {
                // Iterate until enough samples available for output:
                // - read samples from input stream
                // - put samples to SoundStretch processor
                while (st.AvailableSamples < count)
                {
                    int nbytes = inputStr.Read(bytebuffer, 0, bytebuffer.Length);
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
                return numsamples * 8;  // number of bytes
            }
            catch (Exception exp)
            {
                StatusMessage.Write("exception in WaveStreamProcessor.Read: " + exp.Message);
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
    }
}
