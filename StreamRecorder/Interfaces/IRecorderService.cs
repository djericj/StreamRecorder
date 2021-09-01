using StreamRecorder.Domain;

namespace StreamRecorder.Interfaces
{
    public interface IRecorderService
    {
        void Record(string path);

        void Play();

        void Pause();

        void UnPause();

        void Stop();

        void Save(Show show);
    }
}