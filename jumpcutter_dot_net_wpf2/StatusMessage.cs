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

using System;

namespace jumpcutter_dot_net_wpf2
{
    /// <summary>
    /// Helper class that allow writing status texts to the host application
    /// </summary>
    public class StatusMessage
    {
        /// <summary>
        /// Handler for status message events. Subscribe this from the host application
        /// </summary>
        public static event EventHandler<string> StatusEvent;

        /// <summary>
        /// Pass a status message to the host application
        /// </summary>
        public static void Write(string msg)
        {
            StatusEvent?.Invoke(null, msg);
        }
    }
}
