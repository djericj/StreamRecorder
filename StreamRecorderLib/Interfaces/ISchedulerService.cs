using StreamRecorderLib.Domain;
using System;
using System.Threading.Tasks;

namespace StreamRecorderLib.Interfaces
{
    public interface ISchedulerService
    {
        event EventHandler<Show> ShowStarted;

        event EventHandler<Show> ShowEnded;

        Task Start();

        Task Stop();
    }
}