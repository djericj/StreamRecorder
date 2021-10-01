using Microsoft.VisualStudio.TestTools.UnitTesting;
using StreamRecorder.Converter;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace StreamRecorder.Tests
{
    [TestClass]
    public class ConverterServiceTests
    {
        [TestMethod]
        public void TestConvertWavToMp3()
        {
            var converterService = new ConverterService();
            converterService.Progress += ConverterService_Progress;
            converterService.Message += ConverterService_Message;
            var source = @"D:\temp\05-The Ticket Top 10.wav";
            var target = source.Replace(".wav", ".mp3");
            Assert.IsTrue(File.Exists(source));
            if (File.Exists(target)) File.Delete(target);
            Task.Run(() => converterService.Convert(source, target, NAudio.Lame.LAMEPreset.ABR_320, null));
            Assert.IsTrue(File.Exists(target));
        }

        [TestMethod]
        public void TestConvertAsyncWavToMp3()
        {
            var converterService = new ConverterService();
            converterService.Progress += ConverterService_Progress;
            converterService.Message += ConverterService_Message;
            var source = @"D:\temp\05-The Ticket Top 10.wav";
            var target = source.Replace(".wav", ".mp3");
            Assert.IsTrue(File.Exists(source));
            if (File.Exists(target)) File.Delete(target);
            var task = Task.Run(async () =>
            {
                await converterService.ConvertAsync(source, target, NAudio.Lame.LAMEPreset.ABR_320, null);
                Assert.IsTrue(File.Exists(target), "got here");
            });
            task.Wait();
            Assert.IsTrue(File.Exists(target), "got here 2");
        }

        private void ConverterService_Message(object sender, string e)
        {
            Debug.WriteLine(e);
        }

        private void ConverterService_Progress(object sender, ProgressEventArgs e)
        {
            string msg = string.Format($"Progress: {e.Progress:0.0}%, Output: {e.Output:#,0} bytes, Ratio: 1:{e.Ratio:0.0}");
            Debug.Write(msg);
        }
    }
}