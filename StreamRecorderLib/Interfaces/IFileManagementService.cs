namespace StreamRecorderLib.Interfaces
{
    public interface IFileManagementService
    {
        void CleanUp(int days);
    }
}