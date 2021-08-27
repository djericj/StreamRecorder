using Microsoft.Extensions.Configuration;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

namespace StreamRecorder
{
    public class StreamRecorder
    {
        #region Enums

        public enum StreamingPlaybackState
        {
            Stopped,
            Playing,
            Buffering,
            Paused
        }

        #endregion Enums

        #region Fields

        private BufferedWaveProvider bufferedWaveProvider;
        private IWavePlayer waveOutEvent;
        private volatile StreamingPlaybackState playbackState;
        private volatile bool fullyDownloaded;
        private HttpWebRequest webRequest;
        private System.Timers.Timer timer1;
        private WaveInEvent recorder;
        private WaveRecorderProvider waveRecorderProvider;
        private readonly IConfigurationRoot _config;
        private List<string> _playList;

        #endregion Fields

        #region Events

        public event EventHandler<StreamEventMessage> StreamEventMessage;

        public event EventHandler<BufferProgressEvent> BufferProgressEvent;

        #endregion Events

        #region Properties

        public StreamingPlaybackState PlaybackState => playbackState;
        public bool RecordOn { get; private set; }
        public string RecordPath { get; private set; }

        #endregion Properties

        public StreamRecorder(IConfigurationRoot config, string savePath)
        {
            _config = config;
            RecordOn = Convert.ToBoolean(config.GetSection("AppConfig:RecordOn").Value);
            RecordPath = savePath;

            _playList = ReadPlaylist() as List<string>;

            timer1 = new System.Timers.Timer();
            timer1.Interval = 5000;
            timer1.Elapsed += Timer1_Elapsed;
            timer1.Start();
        }

        #region Public Methods

        public void Play()
        {
            var streamUrl = _playList.FirstOrDefault();
            if (playbackState == StreamingPlaybackState.Stopped)
            {
                playbackState = StreamingPlaybackState.Buffering;
                bufferedWaveProvider = null;
                ThreadPool.QueueUserWorkItem(StreamMp3, streamUrl);
                timer1.Enabled = true;
            }
            else if (playbackState == StreamingPlaybackState.Paused)
            {
                playbackState = StreamingPlaybackState.Buffering;
            }
        }

        public void Pause()
        {
            playbackState = StreamingPlaybackState.Buffering;
            waveOutEvent.Pause();
            OnStreamEventMessage(new StreamEventMessage(string.Format("Paused to buffer, waveOut.PlaybackState={0}", waveOutEvent.PlaybackState)));
        }

        public void UnPause()
        {
            playbackState = StreamingPlaybackState.Playing;
            waveOutEvent.Play();
        }

        public void Stop()
        {
            if (playbackState != StreamingPlaybackState.Stopped)
            {
                if (!fullyDownloaded)
                    webRequest.Abort();

                playbackState = StreamingPlaybackState.Stopped;
                if (waveOutEvent != null)
                {
                    waveOutEvent.Stop();
                    waveOutEvent.Dispose();
                    waveOutEvent = null;
                }
                if (recorder != null)
                {
                    recorder.StopRecording();
                    recorder.Dispose();
                    recorder = null;
                }
                if (waveRecorderProvider != null)
                {
                    waveRecorderProvider.Dispose();
                }
                timer1.Enabled = false;
                // n.b. streaming thread may not yet have exited
                Thread.Sleep(500);
                ShowBufferState(0);
            }
        }

        #endregion Public Methods

        #region Event Handlers

        private void Timer1_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (playbackState != StreamingPlaybackState.Stopped)
            {
                if (waveOutEvent == null && bufferedWaveProvider != null)
                {
                    OnStreamEventMessage(new StreamEventMessage("Creating WaveOut Device"));
                    waveOutEvent = CreateWaveOutEvent();
                    waveOutEvent.PlaybackStopped += WaveOut_PlaybackStopped;

                    if (RecordOn)
                    {
                        OnStreamEventMessage(new StreamEventMessage("Record On"));
                        // set up the recorder
                        recorder = new WaveInEvent();
                        recorder.DataAvailable += Recorder_DataAvailable;

                        // set up our signal chain
                        waveRecorderProvider = new WaveRecorderProvider(bufferedWaveProvider, RecordPath);

                        // set up playback
                        waveOutEvent.Init(waveRecorderProvider);

                        // begin playback & record
                        recorder.StartRecording();
                    }
                    else
                    {
                        waveOutEvent.Init(bufferedWaveProvider);
                    }
                    //OnBufferProgressEvent(new BufferProgressEvent((double)bufferedWaveProvider.BufferDuration.TotalMilliseconds));
                }
                else if (bufferedWaveProvider != null)
                {
                    var bufferedSeconds = bufferedWaveProvider.BufferedDuration.TotalSeconds;
                    //ShowBufferState(bufferedSeconds);
                    // make it stutter less if we buffer up a decent amount before playing
                    if (bufferedSeconds < 0.5 && playbackState == StreamingPlaybackState.Playing && !fullyDownloaded)
                    {
                        Pause();
                    }
                    else if (bufferedSeconds > 4 && playbackState == StreamingPlaybackState.Buffering)
                    {
                        Start();
                    }
                    else if (fullyDownloaded && bufferedSeconds == 0)
                    {
                        OnStreamEventMessage(new StreamEventMessage("Reached end of stream"));
                        Stop();
                    }
                }
            }
        }

        private void WaveOut_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            OnStreamEventMessage(new StreamEventMessage("Playback Stopped"));
            if (e.Exception != null)
                OnStreamEventMessage(new StreamEventMessage(String.Format("Playback Error {0}", e.Exception.Message)));
        }

        protected virtual void OnStreamEventMessage(StreamEventMessage e)
        {
            EventHandler<StreamEventMessage> handler = StreamEventMessage;
            handler?.Invoke(this, e);
        }

        protected virtual void OnBufferProgressEvent(BufferProgressEvent e)
        {
            EventHandler<BufferProgressEvent> handler = BufferProgressEvent;
            handler?.Invoke(this, e);
        }

        private void Recorder_DataAvailable(object sender, WaveInEventArgs e)
        {
            bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);
        }

        #endregion Event Handlers

        #region Private Methods

        private void Start()
        {
            waveOutEvent.Play();
            OnStreamEventMessage(new StreamEventMessage(string.Format("Started playing, waveOut.PlaybackState={0}", waveOutEvent.PlaybackState)));
            playbackState = StreamingPlaybackState.Playing;
        }

        private delegate void ShowErrorDelegate(string message);

        private void ShowError(string message) => OnStreamEventMessage(new StreamEventMessage(message));

        private void ShowBufferState(double totalSeconds) => OnBufferProgressEvent(new BufferProgressEvent(totalSeconds));

        private IWavePlayer CreateWaveOutEvent() => new WaveOutEvent();

        private bool IsBufferNearlyFull => bufferedWaveProvider != null &&
                       bufferedWaveProvider.BufferLength - bufferedWaveProvider.BufferedBytes
                       < bufferedWaveProvider.WaveFormat.AverageBytesPerSecond / 4;

        private void StreamMp3(object state)
        {
            fullyDownloaded = false;
            var url = (string)state;
            OnStreamEventMessage(new StreamEventMessage("Connecting to " + url));
            webRequest = (HttpWebRequest)WebRequest.Create(url);
            HttpWebResponse resp;
            try
            {
                resp = (HttpWebResponse)webRequest.GetResponse();
            }
            catch (WebException e)
            {
                if (e.Status != WebExceptionStatus.RequestCanceled)
                {
                    ShowError(e.Message);
                }
                return;
            }
            var buffer = new byte[16384 * 4]; // needs to be big enough to hold a decompressed frame

            IMp3FrameDecompressor decompressor = null;
            try
            {
                using (var responseStream = resp.GetResponseStream())
                {
                    var readFullyStream = new ReadFullyStream(responseStream);
                    do
                    {
                        if (IsBufferNearlyFull)
                        {
                            //OnStreamEventMessage(new StreamEventMessage("Buffer getting full, taking a break"));
                            Thread.Sleep(500);
                        }
                        else
                        {
                            Mp3Frame frame;
                            try
                            {
                                frame = Mp3Frame.LoadFromStream(readFullyStream);
                            }
                            catch (EndOfStreamException)
                            {
                                fullyDownloaded = true;
                                // reached the end of the MP3 file / stream
                                OnStreamEventMessage(new StreamEventMessage("Stream ended."));
                                break;
                            }
                            catch (WebException)
                            {
                                // probably we have aborted download from the GUI thread
                                OnStreamEventMessage(new StreamEventMessage("Aborted download."));
                                break;
                            }
                            if (frame == null) break;
                            if (decompressor == null)
                            {
                                // don't think these details matter too much - just help ACM select the right codec
                                // however, the buffered provider doesn't know what sample rate it is working at
                                // until we have a frame
                                decompressor = CreateFrameDecompressor(frame);
                                bufferedWaveProvider = new BufferedWaveProvider(decompressor.OutputFormat);
                                bufferedWaveProvider.BufferDuration =
                                    TimeSpan.FromSeconds(20); // allow us to get well ahead of ourselves
                                                              //this.bufferedWaveProvider.BufferedDuration = 250;
                            }
                            int decompressed = decompressor.DecompressFrame(frame, buffer, 0);
                            //Debug.WriteLine(String.Format("Decompressed a frame {0}", decompressed));
                            bufferedWaveProvider.AddSamples(buffer, 0, decompressed);
                        }
                    } while (playbackState != StreamingPlaybackState.Stopped);
                    OnStreamEventMessage(new StreamEventMessage("Exiting"));
                    // was doing this in a finally block, but for some reason
                    // we are hanging on response stream .Dispose so never get there
                    decompressor.Dispose();
                }
            }
            finally
            {
                if (decompressor != null)
                {
                    decompressor.Dispose();
                }
            }
        }

        private static IMp3FrameDecompressor CreateFrameDecompressor(Mp3Frame frame)
        {
            WaveFormat waveFormat = new Mp3WaveFormat(frame.SampleRate, frame.ChannelMode == ChannelMode.Mono ? 1 : 2,
                frame.FrameLength, frame.BitRate);
            return new AcmMp3FrameDecompressor(waveFormat);
        }

        private IEnumerable<string> ReadPlaylist()
        {
            var playlistFile = _config.GetSection("AppConfig:Playlist").Value;
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, playlistFile);
            string line;
            List<string> urls = new List<string>();
            StreamReader file = new StreamReader(path);
            while ((line = file.ReadLine()) != null)
                if (line.StartsWith("File"))
                    urls.Add(line.Substring(line.IndexOf("=") + 1));

            file.Close();

            return urls;
        }

        #endregion Private Methods
    }
}