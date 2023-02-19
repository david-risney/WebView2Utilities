using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace wv2util
{
    public static class ReportCreator
    {
        public static string GenerateReportFileName(string hostAppExeName)
        {
            return hostAppExeName + ".WebView2Utilities.Report." + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".zip";
        }

        public class SummaryInfo
        {
            public string WebView2UtilitiesVersion { get; set; }
            public string CreationDate { get; set; }
        }

        private static async Task WriteObjectToZipArchiveEntryAsync(ZipArchive destinationAsZipArchive, Object obj, string entryName)
        {
            ZipArchiveEntry archiveEntry = destinationAsZipArchive.CreateEntry(entryName);
            using (StreamWriter streamWriter = new StreamWriter(archiveEntry.Open()))
            {
                string json = JsonSerializer.Serialize(obj, new JsonSerializerOptions() { WriteIndented = true });
                await streamWriter.WriteAsync(json);
            }
        }

        private static async Task WriteFileToZipArchiveEntryAsync(ZipArchive destinationAsZipArchive, string rawFilePath, string destinationPathPrefix, string fileNamePrefix = "")
        {
            string filePath = Environment.ExpandEnvironmentVariables(rawFilePath);
            if (File.Exists(filePath))
            {
                using (FileStream fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    ZipArchiveEntry archiveEntry = destinationAsZipArchive.CreateEntry(
                        Path.Combine(destinationPathPrefix, fileNamePrefix + Path.GetFileName(filePath)));
                    using (Stream archiveStream = archiveEntry.Open())
                    {
                        await fileStream.CopyToAsync(archiveStream);
                    }
                }
            }
        }

        public static Task CreateReportAsync(string destinationPath, HostAppEntry hostAppEntry, IEnumerable<AppOverrideEntry> appOverrideList, IEnumerable<RuntimeEntry> runtimeList)
        {
            return Task.Run(async () =>
            {
                using (FileStream destinationAsFileStream = new FileStream(destinationPath, FileMode.Create))
                {
                    using (ZipArchive destinationAsZipArchive = new ZipArchive(destinationAsFileStream, ZipArchiveMode.Create))
                    {
                        await WriteObjectToZipArchiveEntryAsync(destinationAsZipArchive, new SummaryInfo
                        {
                            WebView2UtilitiesVersion = VersionUtil.GetWebView2UtilitiesVersion(),
                            CreationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        }, "summary.json");
                        await WriteObjectToZipArchiveEntryAsync(destinationAsZipArchive, hostAppEntry, "hostApp.json");
                        await WriteObjectToZipArchiveEntryAsync(destinationAsZipArchive, appOverrideList, "appOverrideList.json");
                        await WriteObjectToZipArchiveEntryAsync(destinationAsZipArchive, runtimeList, "runtimeList.json");

                        if (hostAppEntry.UserDataPath != null && hostAppEntry.UserDataPath.Length > 0)
                        {
                            // Add crashpad dumps
                            {
                                string crashpadReportFolder = Path.Combine(hostAppEntry.UserDataPath, "Crashpad", "reports");
                                // Get all the files in the crashpad report folder
                                string[] crashpadReportFiles = Directory.GetFiles(crashpadReportFolder);
                                foreach (string crashpadReportFile in crashpadReportFiles)
                                {
                                    // Add the file to the zip archive
                                    await WriteFileToZipArchiveEntryAsync(destinationAsZipArchive, crashpadReportFile, "CrashpadReports");
                                }
                            }

                            // Add log files
                            {
                                string logFolder = hostAppEntry.UserDataPath;
                                string[] logFiles = Directory.GetFiles(logFolder, "*.log");
                                foreach (string logFile in logFiles)
                                {
                                    await WriteFileToZipArchiveEntryAsync(destinationAsZipArchive, logFile, "logs");
                                }
                            }
                        }

                        {
                            (string, string)[] logNames = new (string, string)[]
                            {
                                (@"%temp%\msedge_installer.log", "temp"),
                                (@"%systemroot%\Temp\msedge_installer.log", "systemroot"),
                                (@"%localappdata%\Temp\msedge_installer.log", @"localappdata_temp"),
                                (@"%localappdata%\Temp\MicrosoftEdgeUpdate.log", @"localappdata_Temp"),
                                (@"%ALLUSERSPROFILE%\Microsoft\EdgeUpdate\Log\MicrosoftEdgeUpdate.log", @"ALLUSERSPROFILE_Microsoft_EdgeUpdate_Log"),
                                (@"%ProgramData%\Microsoft\EdgeUpdate\Log\MicrosoftEdgeUpdate.log", @"ProgramData_Microsoft_EdgeUpdate_Log")
                            };
                            foreach (var logName in logNames)
                            {
                                await WriteFileToZipArchiveEntryAsync(destinationAsZipArchive, logName.Item1, "logs", logName.Item2);
                            }
                        }
                    }
                }
            });
        }
    }
}
