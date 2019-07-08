namespace Varispeed.SoundTouch
{
    public class SoundTouchProfile
    {
        public bool UseTempo { get; set; }
        public bool UseAntiAliasing { get; set; }
        public bool UseQuickSeek { get; set; }
        public int SampleRate { get; set; }
        public int Channels { get;  set; }
        public int Size { get; internal set; }
        public SoundTouchProfile(bool useTempo, bool useAntiAliasing)
        {
            UseTempo = useTempo;
            UseAntiAliasing = useAntiAliasing;
        }
    }
}