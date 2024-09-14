using System;
using System.Diagnostics;
using System.Reflection;

namespace WebView2Utilities.Core.Models;

public static class VersionUtil
{
    public static FileVersionInfo TryGetVersionFromFilePath(string filePath)
    {
        if (filePath != "" && filePath != null)
        {
            try
            {
                return FileVersionInfo.GetVersionInfo(filePath);
            }
            catch (Exception)
            {
            }
        }
        return null;
    }

    public static string GetVersionStringFromFilePath(string filePath)
    {
        if (filePath != "" && filePath != null)
        {
            try
            {
                return FileVersionInfo.GetVersionInfo(filePath).FileVersion;
            }
            catch (FileNotFoundException)
            {
                return "File not found";
            }
            catch (ArgumentException)
            {
                return "File not found";
            }
        }
        return "Unknown";

    }

    public static string GetWebView2UtilitiesVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version.ToString();
    }
}
