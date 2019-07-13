using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jumpcutter_dot_net
{
    internal class Utils
    {

        internal void reportStatus(string message, int current, int total, int decimalPresision = 1, string additonal = null, bool last = false)
        {

            var rate = 100 + (decimalPresision * 1000);
            var updateStatus = total / rate;
            updateStatus = updateStatus > 0 ? updateStatus : 1;

            if (current % updateStatus == 0 || last)
            {
                var pc = ((double)current / total);
                if (string.IsNullOrEmpty(additonal))
                {
                    Console.Write(last ? "" : "\r"
                        + string.Format(message, current, total, "(" + pc.ToString("0." + String.Concat(Enumerable.Repeat("0", decimalPresision)) + "%)")));
                }
                else
                {
                    if(last)
                    {
                        Console.WriteLine("\r");
                    }
                    Console.Write(last ? "" : "\r"
          + string.Format(message,additonal, current, total, "(" + pc.ToString("0." + String.Concat(Enumerable.Repeat("0", decimalPresision)) + "%)")));
                }
            }

            if (last)
            {
                Console.WriteLine();
            }

        }

        public double Remap(double value, double from1, double to1, double from2, double to2)
        {
            return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
        }

    }

    internal class Chunk
    {
        public int startFrame;
        public int endFrame;
        public bool hasLoudAudio;

        internal double getSpeed(Options options) => (hasLoudAudio ? options.sounded_speed : options.silent_speed);

        public Chunk(int startFrame, int endFrame, bool hasLoudAudio)
        {
            this.startFrame = startFrame;
            this.endFrame = endFrame;
            this.hasLoudAudio = hasLoudAudio;
        }
    }
}
