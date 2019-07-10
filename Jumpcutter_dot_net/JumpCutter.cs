using System.IO;
using System;
using System.Globalization;
using Emgu.CV;
using System.Collections.Generic;
using Xabe.FFmpeg;
using NAudio.Wave;
using System.Linq;
namespace Jumpcutter_dot_net
{
    internal class JumpCutter
    {
        private Options options;
        private FileInfo videoInputFile;
        private FileInfo videoOutputFile;

        private VideoCapture inputVideo;
        private VideoWriter outputVideo;

        private string tempVideo;
        private string tempAudio;



        public JumpCutter(Options options)
        {
            this.options = options;


            //Move all the below from the constructor
            if (options.download_ffmpeg == true)
            {
                //Download FFMPEG
                Console.WriteLine("Getting Latest FFmpeg...");
                var ffmpeg = FFmpeg.GetLatestVersion();
                try
                {
                    ffmpeg.Wait();

                    if (ffmpeg.Status == System.Threading.Tasks.TaskStatus.RanToCompletion)
                    {
                        Console.WriteLine("FFMpeg is Up to date");
                    }
                    else
                    {
                        throw new JCException("Download FFMpeg Task Failed to complete");
                    }
                }
                catch (Exception e)
                {
                    throw new JCException("Could not download FFmpeg, please allow this application network access, or copy the ffmpeg.exe and ffprobe.exe to the bin dir", e);
                }
            }


            //Check the input file and output file
            handleInputOutputAndTemp();

            //Init the video object
            inputVideo = new VideoCapture(videoInputFile.FullName);

            //Get the video framerate 
            Console.Write("Getting Video Data... ");
            getVideoFrameData();

            //Prepare the audio
            Console.WriteLine("Extracting Audio...");
            prepareAudio();

            //Process the audio
            Console.WriteLine("Processing Audio...");
            var framesToRender = processAudio();


            Console.WriteLine("Processing video...");
            //Init output video
            using (outputVideo = new VideoWriter(tempVideo, Options.VIDEO_CODEC, (double)options.frame_rate, options.frame_size, true))
            {
                outputVideo.Set(VideoWriter.WriterProperty.Quality, options.frame_quality / 100.00);
                writeFinalVideo(framesToRender, "");
            }

            addAudioToVideo();

        }

        private List<int> processAudio()
        {

            HashSet<int> framesToRender = new HashSet<int>();
            float maxVolume = 0;
            List<bool> hasLoudAudio = new List<bool>();

            using (var audioFileReader = new AudioFileReader(tempAudio))
            {
                //Sample rate of the audio file
                var sampleRate = audioFileReader.WaveFormat.SampleRate;

                //Samples of audio per frame
                var samplesPerFrame = (int)((sampleRate * audioFileReader.WaveFormat.Channels) / (double)options.frame_rate);
                List<float> maxVolumePerFrame = new List<float>();

                //Get Max Volume for each frame
                float[] sampleBuffer = new float[samplesPerFrame];
                int samplesRead;

                ///TODO Move volume reading to own function
                var loopCount = 0;
                while ((samplesRead = audioFileReader.Read(sampleBuffer, 0, samplesPerFrame)) > 0)
                {
                    loopCount++;

                    reportStatus("Reading frame volume {0} out of {1} {2}", loopCount, options.frame_count, 0);

                    if (samplesRead != samplesPerFrame)
                    {
                        sampleBuffer = sampleBuffer.Take(samplesRead).ToArray();
                    }

                    var currentMax = getMaxVolume(sampleBuffer);
                    maxVolumePerFrame.Add(currentMax);

                    //If the current frame is less than the max volume for the whole track, increase it
                    if (maxVolume < currentMax)
                    {
                        maxVolume = currentMax;
                    }

                }


                reportStatus("Reading frame volume {0} out of {1} {2}", options.frame_count, options.frame_count, last: true);


                //If the audio track is longer than video track, trim the front of the audio track (Does this always work)?
                var trackLengthDifference = maxVolumePerFrame.Count - options.frame_count;
                if (trackLengthDifference > 0)
                {
                    maxVolumePerFrame.RemoveRange(0, trackLengthDifference);
                }

                //Normalize the maxVolumePerFrame and create a has loud audio list
                ///TODO make sure this isnt doing anything more than 5 frames (ish)
                foreach (var maxVol in maxVolumePerFrame)
                {
                    hasLoudAudio.Add(maxVol / maxVolume >= options.silent_threshold);
                }


                var audioFrameCount = hasLoudAudio.Count;

                ///TODO move build chunks to own function
                //Build Chunks
                List<bool> audioLevelHigh = new List<bool>();

                List<Tuple<int, int, bool>> chunks = new List<Tuple<int, int, bool>>() { Tuple.Create(0, 0, false) };


                for (var i = 0; i < audioFrameCount; i++)
                {
                    var startIndex = (i - options.frame_margin);
                    var count = 1 + options.frame_margin;

                    if (startIndex + count > audioFrameCount - 1) count = (audioFrameCount - startIndex) - 1;
                    if (startIndex < 0) startIndex = 0;

                    audioLevelHigh.Add(hasLoudAudio.GetRange(startIndex, count).Any(p => p == true));

                    if (i >= 1 && audioLevelHigh[i] != audioLevelHigh[i - 1])
                    { // Did we flip?
                        chunks.Add(Tuple.Create(chunks.Last().Item2, i, audioLevelHigh[i - 1]));
                    }

                }
                //Add the last block
                chunks.Add(Tuple.Create(chunks.Last().Item2, audioFrameCount, audioLevelHigh[(audioFrameCount - 1) - 1]));
                chunks.RemoveAt(0);

                //Reset the stream 
                audioFileReader.Position = 0;


                float totalDuration = 0;
                //Caulucate duration difference 
                foreach (var chunk in chunks)
                {
                    var chunkLength = chunk.Item2 - chunk.Item1;
                    var playbackRate = (float)(chunk.Item3 ? options.silent_speed : options.sounded_speed);
                    var duration = chunkLength / playbackRate;
                    totalDuration += duration;

                }

                var durationChange = 100 - (int)((((totalDuration / options.frame_count)) * 100));
                Console.WriteLine("Decreased length of video by: " + durationChange + "%");
                Console.WriteLine("Old duration " + options.frame_count);
                Console.WriteLine("New Duration: " + (int)totalDuration);

                ///TODO process chunks to own function
                //Process Chunks
                using (var writer = new WaveFileWriter(options.temp_dir + @"\finalAudio.wav", audioFileReader.WaveFormat))
                {

                    var outputPointer = 0;
                    foreach (var chunk in chunks)
                    {

                        var audoFrameLength = chunk.Item2 - chunk.Item1;
                        var chunkLength = ((chunk.Item2 * samplesPerFrame) - (chunk.Item1 * samplesPerFrame));
                        int totalSamplesRead = 0;
                        var playbackRate = (float)(chunk.Item3 ? options.sounded_speed : options.silent_speed);


                        int bufferSize = chunkLength < 6750 ? chunkLength : 6750;


                        var writeCount = 0;
                        float[] inputBuffer = new float[bufferSize];
                        float[] outputBuffer = new float[bufferSize];
                        samplesRead = 0;

                        ///TODO Create the soundtouch outside this loop, and just reset it!!!!!!
                        //////TODO move soundtouch processing to own function
                        SoundTouch.SoundTouch<float, double> soundTouch
                         = new SoundTouch.SoundTouch<float, double>();

                        soundTouch.SetRate(1.0f);
                        soundTouch.SetPitchOctaves(0f);
                        soundTouch.SetTempo(0);
                        soundTouch.SetTempoChange((playbackRate - 1) * 100);
                        soundTouch.SetSampleRate((int)(audioFileReader.WaveFormat.SampleRate));
                        soundTouch.SetChannels(audioFileReader.WaveFormat.Channels);
                        var readAll = false;
                        var nSamples = 0;


                        while (!readAll || soundTouch.AvailableSamples > 0)
                        {
                            if (!readAll)
                            {
                                reportStatus("Writing new audio {0} out of {1} {2}", chunk.Item1 + (totalSamplesRead / samplesPerFrame), audioFrameCount, 2);


                                samplesRead = audioFileReader.Read(inputBuffer, 0, bufferSize);
                                if (samplesRead == 0 || totalSamplesRead >= chunkLength)
                                {
                                    readAll = true;

                                    soundTouch.Flush();
                                }
                                else
                                {
                                    nSamples = samplesRead / audioFileReader.WaveFormat.Channels;
                                    soundTouch.PutSamples(inputBuffer, nSamples);
                                }
                            }

                            do
                            {
                                nSamples = writeCount = soundTouch.ReceiveSamples(outputBuffer, bufferSize / audioFileReader.WaveFormat.Channels);

                                writer.WriteSamples(outputBuffer, 0, nSamples * audioFileReader.WaveFormat.Channels);

                            } while (nSamples != 0);


                            totalSamplesRead += samplesRead;

                        }




                        ////TODO Check that this is right
                        var endPointer = outputPointer + totalSamplesRead;
                        var startOutputFrame = outputPointer / samplesPerFrame;
                        var endOutputFrame = endPointer / samplesPerFrame;


                        for (var outputFrame = startOutputFrame; outputFrame <= endOutputFrame; outputFrame++)
                        {
                            var inputFrame = (int)(chunk.Item1 + playbackRate * (outputFrame - startOutputFrame));
                            framesToRender.Add(inputFrame);
                        }


                        outputPointer = endPointer;




                        soundTouch.Clear();
                        soundTouch = null;
                    }
                }

                reportStatus("Writing new audio {0} out of {1} {2}", chunks.Count, chunks.Count, 2, true);
            }

            return new List<int>(framesToRender);
        }

        private float getMaxVolume(float[] s)
        {
            var maxv = s.Max();
            var minv = -s.Min();
            return new float[] { maxv, minv }.Max();
        }


        private void prepareAudio()
        {

            ///TODO write progress
            //Get the audio file from the video
            /////DEBUG 
            ///
            if (!File.Exists(tempAudio))
            {
                var conv = Conversion.ExtractAudio(videoInputFile.FullName, tempAudio);
                conv.Start().Wait();
            }
            else
            {
                //Console.WriteLine("DEBUG.... SKIPPING CONVERSION!!!");
            }
        }


        private void writeFinalVideo(List<int> framesToRender, string audioFile)
        {

            //Update status every x frames, with a 0.1% rate
            var lastFrame = 0;
            var count = 0;

            foreach (var frame in framesToRender)
            {
                reportStatus("Writing frame {0} out of {1} {2}", count, framesToRender.Count, 2);
                count++;
                if (frame != lastFrame)
                {
                    var framesToMove = frame - lastFrame;
                    bool nextFrame;
                    //move to next x frame
                    for (var i = 0; i < framesToMove; i++)
                    {
                        nextFrame = inputVideo.Grab();
                    }
                    ///TODO Double check that nextFrame exsits?
                    ///
                    var img = new Mat();
                    inputVideo.Retrieve(img);

                    outputVideo.Write(img);
                    img.Dispose();


                }
                lastFrame = frame;

            }
            reportStatus("Writing frame {0} out of {1} {2}", options.frame_count, options.frame_count, 2, true);

        }

        private void addAudioToVideo()
        {
            ///TODO write progress
            ///TODO move this away from FFMpeg to native
            Console.WriteLine("Joining video and audio...");
            //Concat the audio and video
            var tempAudio = options.temp_dir + @"\" + "finalAudio.wav";

            var conv = Conversion.AddAudio(tempVideo, tempAudio, videoOutputFile.FullName);
            conv.Start().Wait();

        }

        private void handleInputOutputAndTemp()
        {

            //Does the extention end .mp4, if so we will assume the file is a mp4 file
            if (!options.input_file.EndsWith(".mp4", true, CultureInfo.CurrentCulture))
            {
                throw new JCException("This application only supports MP4 as an input file");
            }

            //Open the input file
            videoInputFile = new FileInfo(options.input_file);
            //Does it exist?
            if (!videoInputFile.Exists) throw new JCException("File " + options.input_file + " doesn't exist");

            //Setup output file
            if (options.output_file != null)
            {
                //Does the extention end .mp4, if so we will assume the file is a mp4 file
                if (!options.input_file.EndsWith(".mp4", true, CultureInfo.CurrentCulture)) throw new JCException("This application only supports MP4 as an output file");

                videoOutputFile = new FileInfo(options.output_file);

            }
            else
            {
                videoOutputFile = new FileInfo(videoInputFile.DirectoryName + "/" + Path.GetFileNameWithoutExtension(videoInputFile.Name) + "_ALTERED.mp4");
            }

            //If the output file already exists, delete it, if debug mode
            if (videoOutputFile.Exists)
            {
                ///TODO Add param for overrite file
                if (true)
                {
                    videoOutputFile.Delete();
                }
                else
                {
                    throw new JCException("File " + options.output_file + " already exists");
                }

            }

            try
            {
                ///TODO FIX THIS
                //Make sure we can create and write the file
                videoOutputFile.Directory.Create();
                //videoOutputFile.Create();
                //videoOutputFile.Delete();
            }
            catch (Exception e) { throw new JCException("Unable to create output file " + options.output_file + "! ", e); }

            var tempdir = new DirectoryInfo(videoOutputFile.Directory + @"\" + videoInputFile.Name + "_temp");
            options.temp_dir = tempdir.FullName;

            //Clear the temp dir
            ////TODO DEBUG
            if (tempdir.Exists)
            {
                //tempdir.Delete(true);
            }
            else
            {
                tempdir.Create();
            }


            tempVideo = options.temp_dir + @"\" + "video_no_audio.mp4";
            tempAudio = options.temp_dir + @"\" + "fullaudio.wav";
        }

        private void getVideoFrameData()
        {
            if (options.frame_rate == null)
            {
                options.frame_rate = inputVideo.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.Fps);
            }


            var frameWidth = (int)inputVideo.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameWidth);
            var frameHeight = (int)inputVideo.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameHeight);
            var frameCount = (int)(inputVideo.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FrameCount)); ;
            var videoLength = (int)(frameCount / options.frame_rate);

            //Just to avoid numbers like 29.99999954
            //frameRate = Math.Round(frameRate, 4);


            options.frame_count = frameCount;
            options.orignial_length = videoLength;
            options.frame_size = new System.Drawing.Size(frameWidth, frameHeight);

            //Only need to do this to have the same input codec as output codec, we will be using MP4 so we omit this
            //var codec = (int)(inputVideo.GetCaptureProperty(Emgu.CV.CvEnum.CapProp.FourCC));
            //options.video_codec = codec;


            Console.WriteLine(options.frame_rate + " fps | " + options.orignial_length + " seconds | " + options.frame_count + " frames | " + options.frame_size.Height + "p");

        }


        private void reportStatus(string message, int current, int total, int decimalPresision = 1, bool last = false)
        {

            var rate = 100 + (decimalPresision * 1000);
            var updateStatus = total / rate;
            updateStatus = updateStatus > 0 ? updateStatus : 1;

            if (current % updateStatus == 0)
            {
                var pc = ((double)current / total);
                Console.Write("\r"
                    + String.Format(message, current, total,
                    "(" + pc.ToString("0." + String.Concat(Enumerable.Repeat("0", decimalPresision)) + "%)")));
            }

            if (last)
            {
                Console.WriteLine();
            }
        }

        //This function is slower than streaming the whole file and dropping un-needed frames
        private Mat getFrameAt(int frameNumber)
        {
        
            inputVideo.SetCaptureProperty(Emgu.CV.CvEnum.CapProp.PosFrames, frameNumber);
            return inputVideo.QueryFrame();
        }

    }
}