using CommandLine;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jumpcutter_dot_net
{
    public class Options
    {
        //[Option('u', "input_file", Required = true, HelpText = "The video file or youtube URL you want modified")]
        [Option('u', "input_file", Required = true, HelpText = "The video file you want modified")]
        public string input_file { get; set; }


        [Option(Required = false, HelpText = "The output file. (If not included,it'll just modify the input file name)")]
        public string output_file { get; set; }


        [Option(Required = false, Default = 0.03 ,HelpText = "The volume amount that frames' audio needs to surpass to be consider \"sounded\". It ranges from 0 (silence) to 1 (max volume)")]
        public double silent_threshold { get; set; }


        [Option(Required = false, Default = 1, HelpText = "The speed that sounded (spoken) frames should be played at. Typically 1.")]
        public double sounded_speed { get; set; }


        [Option(Required = false,  Default = 5, HelpText = "the speed that silent frames should be played at. 999999 for jumpcutting.")]
        public double silent_speed { get; set; }


        [Option(Required = false, Default = 1, HelpText  = "Some silent frames adjacent to sounded frames are included to provide context. How many frames on either the side of speech should be included? That's this variable.")]
        public int frame_margin { get; set; }


        [Option(Required = false, HelpText = "Frame rate override the detected rate of the input and output videos.")]
        public double? frame_rate { get; set; }


        [Option(Required = false, Default = 75, HelpText = "Quality of frames to be extracted from input video.")]
        public int frame_quality { get; set; }

        [Option(Required = false, Default = true, HelpText = "Download FFmpeg before starting (If set to false place FFMpeg.exe and FFProbe.exe in bin dir)")]
        public bool download_ffmpeg { get; set; }




        public int orignial_length;
        public int frame_count;
        public Size frame_size;

        //VideoWriter.Fourcc('m', 'p', '4', 'v');
       
        public string temp_dir;


        //smooth out transitiion's audio by quickly fading in/out (arbitrary magic number whatever)
        public const int AUDIO_FADE_ENVELOPE_SIZE = 400;
        public const int VIDEO_CODEC = 1983148141;
        //internal const int SAMPLE_RATE = 44100;




    }
}