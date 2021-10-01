using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NAudio.Wave;
using StreamRecorderLib.Domain;
using StreamRecorderLib.Events;
using StreamRecorderLib.Interfaces;
using StreamRecorderLib.Providers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

namespace StreamRecorderLib.Services
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

        private readonly IOptions<AppSettings> _appSettings;
        private readonly System.Timers.Timer timer1;

        private volatile StreamingPlaybackState playbackState;
        private volatile bool fullyDownloaded;

        private BufferedWaveProvider bufferedWaveProvider;
        private IWavePlayer waveOutEvent;
        private HttpWebRequest webRequest;
        private WaveInEvent recorder;
        private WaveRecorderProvider waveRecorderProvider;
        private string _recordPath;
        private readonly List<string> _playList;

        public event EventHandler<RecorderEventArgs> RecorderEvent;

        public event EventHandler<RecorderExceptionArgs> RecorderException;

        #endregion Fields

        #region Properties

        public StreamingPlaybackState PlaybackState => playbackState;

        #endregion Properties

        public StreamRecorderService(IOptions<AppSettings> appSettings)
        {
            _appSettings = appSettings;

            _playList = _appSettings.Value.Station.Playlist;

            timer1 = new System.Timers.Timer
            {
                Interval = 5000
            };
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
            RecorderEvent?.Invoke(this,
                new RecorderEventArgs(string.Format("Paused to buffer, waveOut.PlaybackState={0}", waveOutEvent.PlaybackState),
                LogLevel.Information));
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
                    RecorderEvent?.Invoke(this,
                        new RecorderEventArgs("Creating WaveOut Device",
                        LogLevel.Information));
                    waveOutEvent = CreateWaveOutEvent();
                    waveOutEvent.PlaybackStopped += WaveOut_PlaybackStopped;

                    if (_appSettings.Value.RecordOn)
                    {
                        RecorderEvent?.Invoke(this,
                            new RecorderEventArgs($"Recording to {_recordPath}",
                            LogLevel.Information));
                        // set up the recorder
                        recorder = new WaveInEvent();
                        recorder.DataAvailable += Recorder_DataAvailable;

                        // set up our signal chain
                        waveRecorderProvider = new WaveRecorderProvider(bufferedWaveProvider, _recordPath);

                        var volumeProvider = new VolumeWaveProvider16(waveRecorderProvider)
                        {
                            Volume = 0
                        };

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
                        RecorderEvent?.Invoke(this,
                            new RecorderEventArgs($"Reached end of stream",
                            LogLevel.Information));
                        Stop();
                    }
                }
            }
        }

        private void WaveOut_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            RecorderEvent?.Invoke(this, new RecorderEventArgs("Playback Stopped", LogLevel.Information));
            //_logger.LogInformation("Playback Stopped");
            if (e.Exception != null)
            {
                //_logger.LogError(string.Format("Playback Error {0}", e.Exception.Message));
                RecorderEvent?.Invoke(this, new RecorderEventArgs(string.Format("Playback Error {0}", e.Exception.Message), LogLevel.Error));
                RecorderException?.Invoke(this, new RecorderExceptionArgs(e.Exception));
            }
        }

        private void Recorder_DataAvailable(object sender, WaveInEventArgs e) =>
            bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);

        #endregion Event Handlers

        #region Private Methods

        private void Start()
        {
            waveOutEvent.Play();
            var operation = !string.IsNullOrEmpty(_recordPath) ?
                                                "recording" :
                                                "playing";
            RecorderEvent?.Invoke(this,
                            new RecorderEventArgs(
                                string.Format($"Started {operation}, waveOut.PlaybackState={waveOutEvent.PlaybackState}"),
                            LogLevel.Information));
            playbackState = StreamingPlaybackState.Playing;
        }

        private delegate void ShowErrorDelegate(string message);

        private void ShowError(string message) => RecorderEvent?.Invoke(this, new RecorderEventArgs(message, LogLevel.Error));

        private IWavePlayer CreateWaveOutEvent() => new WaveOutEvent();

        private bool IsBufferNearlyFull => bufferedWaveProvider != null &&
                       bufferedWaveProvider.BufferLength - bufferedWaveProvider.BufferedBytes
                       < bufferedWaveProvider.WaveFormat.AverageBytesPerSecond / 4;

        private void StreamMp3(object state)
        {
            fullyDownloaded = false;
            var url = (string)state;
            RecorderEvent?.Invoke(this, new RecorderEventArgs($"Connecting to {url}", LogLevel.Information));
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
                using var responseStream = resp.GetResponseStream();
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
                            RecorderEvent?.Invoke(this, new RecorderEventArgs($"Stream ended.", LogLevel.Information));
                            break;
                        }
                        catch (WebException)
                        {
                            // probably we have aborted download from the GUI thread
                            RecorderEvent?.Invoke(this, new RecorderEventArgs($"Aborted download.", LogLevel.Information));
                            break;
                        }
                        if (frame == null) break;
                        if (decompressor == null)
                        {
                            // don't think these details matter too much - just help ACM select the right codec
                            // however, the buffered provider doesn't know what sample rate it is working at
                            // until we have a frame
                            decompressor = CreateFrameDecompressor(frame);
                            bufferedWaveProvider = new BufferedWaveProvider(decompressor.OutputFormat)
                            {
                                BufferDuration =
                                TimeSpan.FromSeconds(20) // allow us to get well ahead of ourselves
                            };
                            //this.bufferedWaveProvider.BufferedDuration = 250;
                        }
                        int decompressed = decompressor.DecompressFrame(frame, buffer, 0);
                        //Debug.WriteLine(String.Format("Decompressed a frame {0}", decompressed));
                        bufferedWaveProvider.AddSamples(buffer, 0, decompressed);
                    }
                } while (playbackState != StreamingPlaybackState.Stopped);

                RecorderEvent?.Invoke(this, new RecorderEventArgs($"Exiting.", LogLevel.Information));

                // was doing this in a finally block, but for some reason
                // we are hanging on response stream .Dispose so never get there
                decompressor.Dispose();
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