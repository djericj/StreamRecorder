using StreamRecorderLib.Domain;
using StreamRecorderLib.Events;
using System;

namespace StreamRecorderLib.Interfaces
{
    public interface IRecorderService
    {
        event EventHandler<RecorderEventArgs> RecorderEvent;

        event EventHandler<RecorderExceptionArgs> RecorderException;

        void Record(string path);

        void Play();

        void Pause();

        void UnPause();

        void Stop();

        void Save(Show show);
    }
}