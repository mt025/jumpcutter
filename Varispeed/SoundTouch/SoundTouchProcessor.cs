namespace Varispeed.SoundTouch
{
    public class SoundTouchProcessor
    {
        private readonly SoundTouch soundTouch;
        private float playbackRate = 1.0f;
        private SoundTouchProfile soundTouchProfile;

        public SoundTouchProcessor(float[] samples, SoundTouchProfile soundTouchProfile)
        {
            soundTouch = new SoundTouch();
            soundTouchProfile.Size = samples.Length;
            SetSoundTouchProfile(soundTouchProfile);
            soundTouch.PutSamples(samples, soundTouchProfile.SampleRate / soundTouchProfile.Channels);
        }

        public float[] Process() {

            soundTouch.Flush();
            float[] outputSamples = new float[soundTouchProfile.Size];
            soundTouch.ReceiveSamples(outputSamples, soundTouchProfile.Size);
            return outputSamples;
        }



        public void SetSoundTouchProfile(SoundTouchProfile soundTouchProfile)
        {

            soundTouch.SetSampleRate(soundTouchProfile.SampleRate);
            soundTouch.SetChannels(soundTouchProfile.Channels);


            if (soundTouchProfile != null &&
                playbackRate != 1.0f &&
                soundTouchProfile.UseTempo != soundTouchProfile.UseTempo)
            {
                if (soundTouchProfile.UseTempo)
                {
                    soundTouch.SetRate(1.0f);
                    soundTouch.SetPitchOctaves(0f);
                    soundTouch.SetTempo(playbackRate);
                }
                else
                {
                    soundTouch.SetTempo(1.0f);
                    soundTouch.SetRate(playbackRate);
                }
            }
            this.soundTouchProfile = soundTouchProfile;
            soundTouch.SetUseAntiAliasing(soundTouchProfile.UseAntiAliasing);
            soundTouch.SetUseQuickSeek(soundTouchProfile.UseQuickSeek);
        }

    }
}