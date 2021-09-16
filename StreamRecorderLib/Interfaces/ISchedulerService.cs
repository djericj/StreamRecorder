using System.Threading.Tasks;

namespace StreamRecorderLib.Interfaces
{
    public interface ISchedulerService
    {
        Task Start();

        Task Stop();
    }
}