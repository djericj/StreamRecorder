using Microsoft.Extensions.Options;
using StreamRecorderLib.Domain;
using StreamRecorderLib.Interfaces;
using System;
using System.IO;
using System.Linq;

namespace StreamRecorderLib.Services
{
    public class FileManagementService : IFileManagementService
    {
        private readonly AppSettings _appSettings;

        public FileManagementService(IOptions<AppSettings> appSettings)
        {
            _appSettings = appSettings.Value;
        }

        public void CleanUp(int days)
        {
            Directory.GetFiles(_appSettings.SaveFolder)
                 .Select(f => new FileInfo(f))
                 .Where(f => f.CreationTime < DateTime.Now.AddDays(days * -1))
                 .ToList()
                 .ForEach(f => f.Delete());

            Directory.GetDirectories(_appSettings.SaveFolder)
                 .Select(f => new FileInfo(f))
                 .Where(f => f.CreationTime < DateTime.Now.AddDays(days * -1))
                 .ToList()
                 .ForEach(f => f.Delete());
        }
    }
}