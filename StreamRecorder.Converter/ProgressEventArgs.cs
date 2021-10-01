using System;

namespace StreamRecorder.Converter
{
    public class ProgressEventArgs : EventArgs
    {
        public double Progress { get; set; }
        public double Output { get; set; }
        public double Ratio { get; set; }

        public ProgressEventArgs(double progress, double output, double ratio)
        {
            Progress = progress;
            Output = output;
            Ratio = ratio;
        }
    }
}