using CommandLine;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jumpcutter_dot_net
{
    class Arguments
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


        [Option(Required = false, Default = 44100, HelpText = "Sample rate of the input and output videos")]
        public int sample_rate { get; set; }


        [Option(Required = false, HelpText = "Frame rate override the detected rate of the input and output videos.")]
        public double? frame_rate { get; set; }


        [Option(Required = false, Default = 75, HelpText = "Quality of frames to be extracted from input video.")]
        public int frame_quality { get; set; }



        internal int orignial_length;
        internal int frame_count;
        internal Size frame_size;

        //VideoWriter.Fourcc('m', 'p', '4', 'v');
        internal int video_codec = 1983148141;

        //smooth out transitiion's audio by quickly fading in/out (arbitrary magic number whatever)
        public const int AUDIO_FADE_ENVELOPE_SIZE = 400;
        


    }
}

//OpenCV: FFMPEG: format mp4 / MP4 (MPEG-4 Part 14)
//fourcc tag 0x7634706d/'mp4v' codec_id 000C
//fourcc tag 0x31637661/'avc1' codec_id 001B
//fourcc tag 0x33637661/'avc3' codec_id 001B
//fourcc tag 0x31766568/'hev1' codec_id 00AD
//fourcc tag 0x31637668/'hvc1' codec_id 00AD
//fourcc tag 0x7634706d/'mp4v' codec_id 0002
//fourcc tag 0x7634706d/'mp4v' codec_id 0001
//fourcc tag 0x7634706d/'mp4v' codec_id 0007
//fourcc tag 0x7634706d/'mp4v' codec_id 003D
//fourcc tag 0x7634706d/'mp4v' codec_id 0058
//fourcc tag 0x312d6376/'vc-1' codec_id 0046
//fourcc tag 0x63617264/'drac' codec_id 0074
//fourcc tag 0x7634706d/'mp4v' codec_id 00A3
//fourcc tag 0x39307076/'vp09' codec_id 00A7
//fourcc tag 0x31307661/'av01' codec_id 801D
//fourcc tag 0x6134706d/'mp4a' codec_id 15002
//fourcc tag 0x6134706d/'mp4a' codec_id 1502D
//fourcc tag 0x6134706d/'mp4a' codec_id 15001
//fourcc tag 0x6134706d/'mp4a' codec_id 15000
//fourcc tag 0x332d6361/'ac-3' codec_id 15003
//fourcc tag 0x332d6365/'ec-3' codec_id 15028
//fourcc tag 0x6134706d/'mp4a' codec_id 15004
//fourcc tag 0x43614c66/'fLaC' codec_id 1500C
//fourcc tag 0x7375704f/'Opus' codec_id 1503C
//fourcc tag 0x6134706d/'mp4a' codec_id 15005
//fourcc tag 0x6134706d/'mp4a' codec_id 15018
//fourcc tag 0x6134706d/'mp4a' codec_id 15803
//fourcc tag 0x7334706d/'mp4s' codec_id 17000
//fourcc tag 0x67337874/'tx3g' codec_id 17005
//fourcc tag 0x646d7067/'gpmd' codec_id 18807
