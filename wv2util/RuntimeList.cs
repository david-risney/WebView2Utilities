using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wv2util
{
    public class RuntimeList
    {
        private IEnumerable<string> GetInstalledRuntimes()
        {
            string microsoftRootPath = Environment.GetEnvironmentVariable("LOCALAPPDATA") + "\\Microsoft\\";
            string[] foundExes = Directory.GetFiles(microsoftRootPath, "msedgewebview2.exe", SearchOption.AllDirectories);
            return foundExes.Select(foundExe => Directory.GetParent(foundExe).FullName);
        }

        private List<string> m_runtimeLocations;
    }
}
