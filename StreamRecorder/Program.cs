using System;

namespace StreamRecorder
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var streamEncoder = new StreamEncoder();
            streamEncoder.StreamEventMessage += StreamEncoder_StreamEventMessage;
            streamEncoder.BufferProgressEvent += StreamEncoder_BufferProgressEvent;
            string url = "http://24863.live.streamtheworld.com:80/KTCKAM_SC";
            streamEncoder.Play(url);
            while (streamEncoder.PlaybackState != StreamEncoder.StreamingPlaybackState.Stopped)
            {
            }
        }

        private static void StreamEncoder_BufferProgressEvent(object sender, BufferProgressEvent e)
        {
            Console.Write("\rBuffer: {0}   ", e.TotalSeconds.ToString());
        }

        private static void StreamEncoder_StreamEventMessage(object sender, StreamEventMessage e)
        {
            Console.WriteLine(e.Message);
        }
    }
}