using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NAudio.Wave;
using StreamRecorder.Domain;
using StreamRecorder.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

namespace StreamRecorder
{
    public class StreamRecorderService : IRecorderService
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

        private ILogger _logger;
        private BufferedWaveProvider bufferedWaveProvider;
        private IWavePlayer waveOutEvent;
        private volatile StreamingPlaybackState playbackState;
        private volatile bool fullyDownloaded;
        private HttpWebRequest webRequest;
        private readonly IOptions<AppSettings> _appSettings;
        private System.Timers.Timer timer1;
        private WaveInEvent recorder;
        private WaveRecorderProvider waveRecorderProvider;
        private List<string> _playList;
        private string _recordPath;

        #endregion Fields

        #region Properties

        public StreamingPlaybackState PlaybackState => playbackState;

        #endregion Properties

        public StreamRecorderService(ILogger<StreamRecorderService> logger, IOptions<AppSettings> appSettings)
        {
            _logger = logger;
            _appSettings = appSettings;

            _playList = _appSettings.Value.Playlist;

            timer1 = new System.Timers.Timer();
            timer1.Interval = 5000;
            timer1.Elapsed += Timer1_Elapsed;
            timer1.Start();
        }

        #region Public Methods

        public void Record(string path)
        {
            _recordPath = path;
            Play();
        }

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
            _logger.LogInformation(string.Format("Paused to buffer, waveOut.PlaybackState={0}", waveOutEvent.PlaybackState));
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
                    waveRecorderProvider.Dispose();

                timer1.Enabled = false;
                // n.b. streaming thread may not yet have exited
                Thread.Sleep(500);
                //ShowBufferState(0);
            }
        }

        public void Save(Show show) => throw new NotImplementedException();

        #endregion Public Methods

        #region Event Handlers

        private void Timer1_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (playbackState != StreamingPlaybackState.Stopped)
            {
                if (waveOutEvent == null && bufferedWaveProvider != null)
                {
                    _logger.LogInformation("Creating WaveOut Device");
                    waveOutEvent = CreateWaveOutEvent();
                    waveOutEvent.PlaybackStopped += WaveOut_PlaybackStopped;

                    if (_appSettings.Value.RecordOn)
                    {
                        _logger.LogInformation($"Recording to {_recordPath}");
                        // set up the recorder
                        recorder = new WaveInEvent();
                        recorder.DataAvailable += Recorder_DataAvailable;

                        // set up our signal chain
                        waveRecorderProvider = new WaveRecorderProvider(bufferedWaveProvider, _recordPath);

                        var volumeProvider = new VolumeWaveProvider16(waveRecorderProvider);
                        volumeProvider.Volume = 0;

                        // set up playback
                        waveOutEvent.Init(volumeProvider);

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
                        _logger.LogInformation("Reached end of stream");
                        Stop();
                    }
                }
            }
        }

        private void WaveOut_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            _logger.LogInformation("Playback Stopped");
            if (e.Exception != null)
                _logger.LogInformation(string.Format("Playback Error {0}", e.Exception.Message));
        }

        private void Recorder_DataAvailable(object sender, WaveInEventArgs e) =>
            bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);

        #endregion Event Handlers

        #region Private Methods

        private void Start()
        {
            waveOutEvent.Play();
            _logger.LogInformation(string.Format("Started playing, waveOut.PlaybackState={0}", waveOutEvent.PlaybackState));
            playbackState = StreamingPlaybackState.Playing;
        }

        private delegate void ShowErrorDelegate(string message);

        private void ShowError(string message) => _logger.LogError(message);

        private void ShowBufferState(double totalSeconds) => _logger.LogInformation($"Buffer Event: {totalSeconds}");

        private IWavePlayer CreateWaveOutEvent() => new WaveOutEvent();

        private bool IsBufferNearlyFull => bufferedWaveProvider != null &&
                       bufferedWaveProvider.BufferLength - bufferedWaveProvider.BufferedBytes
                       < bufferedWaveProvider.WaveFormat.AverageBytesPerSecond / 4;

        private void StreamMp3(object state)
        {
            fullyDownloaded = false;
            var url = (string)state;
            _logger.LogInformation("Connecting to " + url);
            webRequest = (HttpWebRequest)WebRequest.Create(url);
            HttpWebResponse resp;
            try
            {
                resp = (HttpWebResponse)webRequest.GetResponse();
            }
            catch (WebException e)
            {
                if (e.Status != WebExceptionStatus.RequestCanceled)
                    ShowError(e.Message);
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
                                _logger.LogInformation("Stream ended.");
                                break;
                            }
                            catch (WebException)
                            {
                                // probably we have aborted download from the GUI thread
                                _logger.LogInformation("Aborted download.");
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
                    _logger.LogInformation("Exiting");
                    // was doing this in a finally block, but for some reason
                    // we are hanging on response stream .Dispose so never get there
                    decompressor.Dispose();
                }
            }
            finally
            {
                if (decompressor != null)
                    decompressor.Dispose();
            }
        }

        private static IMp3FrameDecompressor CreateFrameDecompressor(Mp3Frame frame)
        {
            WaveFormat waveFormat = new Mp3WaveFormat(frame.SampleRate, frame.ChannelMode == ChannelMode.Mono ? 1 : 2,
                frame.FrameLength, frame.BitRate);
            return new AcmMp3FrameDecompressor(waveFormat);
        }

        #endregion Private Methods
    }
}