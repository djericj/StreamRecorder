using System.Threading.Tasks;

namespace StreamRecorder.Interfaces
{
    public interface ISchedulerService
    {
        Task Start();

        Task Stop();
    }
}