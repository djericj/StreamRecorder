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
            string recordPath = @"C:\Users\djeri\Downloads\KTCK-Test.wav";
            //streamEncoder.Play(url);
            streamEncoder.Record(url, recordPath);
            Console.WriteLine("P = Play/Pause, S = Stop, Q = Quit");
            while (true)
            {
                if (Console.ReadKey().Key == ConsoleKey.P)
                {
                    if (streamEncoder.PlaybackState == StreamEncoder.StreamingPlaybackState.Playing) streamEncoder.Pause();
                    else streamEncoder.Play(url);
                }
                else if (Console.ReadKey().Key == ConsoleKey.S)
                {
                    streamEncoder.Stop();
                }
                else if (Console.ReadKey().Key == ConsoleKey.Q)
                {
                    streamEncoder.Stop();
                    break;
                }
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