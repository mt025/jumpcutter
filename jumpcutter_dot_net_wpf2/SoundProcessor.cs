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
using System;

namespace jumpcutter_dot_net_wpf2
{


    /// <summary>
    /// Class that opens & plays MP3 file and allows real-time audio processing with SoundTouch
    /// while playing
    /// </summary>
    public class SoundProcessor
    {
        WaveFileReader wavFile;
        WaveOut waveOut;
        public WaveStreamProcessor streamProcessor;


        /// <summary>
        /// Start / resume playback
        /// </summary>
        /// <returns>true if successful, false if audio file not open</returns>
        public bool Play()
        {
            if (waveOut == null) return false;

            if (waveOut.PlaybackState != PlaybackState.Playing)
            {
                waveOut.Play();
            }
            return true;
        }


        /// <summary>
        /// Pause playback
        /// </summary>
        /// <returns>true if successful, false if audio not playing</returns>
        public bool Pause()
        {
            if (waveOut == null) return false;

            if (waveOut.PlaybackState == PlaybackState.Playing)
            {
                waveOut.Stop();
                return true;
            }
            return false;
        }


        /// <summary>
        /// Stop playback
        /// </summary>
        /// <returns>true if successful, false if audio file not open</returns>
        public bool Stop()
        {
            if (waveOut == null) return false;

            waveOut.Stop();
            wavFile.Position = 0;
            streamProcessor.Clear();
            return true;
        }



        /// <summary>
        /// Event for "playback stopped" event. 'bool' argument is true if playback has reached end of stream.
        /// </summary>
        public event EventHandler<bool> PlaybackStopped;


        /// <summary>
        /// Proxy event handler for receiving playback stopped event from WaveOut
        /// </summary>
        protected void EventHandler_stopped(object sender, StoppedEventArgs args)
        {
            bool isEnd = streamProcessor.EndReached;
            if (isEnd)
            {
                Stop();
            }
            PlaybackStopped?.Invoke(sender, isEnd);
        }


        /// <summary>
        /// Open Wav file
        /// </summary>
        /// <param name="filePath">Path to file to open</param>
        /// <returns>true if successful</returns>
        public bool OpenWavFile(string filePath)
        {
            try
            {
                wavFile = new WaveFileReader(filePath);
                WaveChannel32 inputStream = new WaveChannel32(wavFile)
                {
                    PadWithZeroes = false  // don't pad, otherwise the stream never ends
                };

                streamProcessor = new WaveStreamProcessor(inputStream);

                waveOut = new WaveOut()
                {
                    DesiredLatency = 100
                };

                waveOut.Init(streamProcessor);  // inputStream);
                waveOut.PlaybackStopped += EventHandler_stopped;

                StatusMessage.Write("Opened file " + filePath);
                return true;
            }
            catch (Exception exp)
            {
                // Error in opening file
                waveOut = null;
                StatusMessage.Write("Can't open file: " + exp.Message);
                return false;
            }

        }
    }
}
