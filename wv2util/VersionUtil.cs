using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wv2util
{
    public static class VersionUtil
    {
        public static string GetVersionStringFromFilePath(string filePath)
        {
            if (filePath != "" && filePath != null)
            {
                try
                {
                    return FileVersionInfo.GetVersionInfo(filePath).FileVersion;
                }
                catch (System.IO.FileNotFoundException)
                {
                    return "File not found";
                }
                catch (System.ArgumentException)
                {
                    return "File not found";
                }
            }
            return "Unknown";

        }
    }
}
