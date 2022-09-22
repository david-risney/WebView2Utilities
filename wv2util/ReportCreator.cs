using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using System.Threading.Tasks;
using System.IO;

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
                string json = JsonSerializer.Serialize(obj);
                await streamWriter.WriteAsync(json);
            }
        }

        private static async Task WriteFileToZipArchiveEntryAsync(ZipArchive destinationAsZipArchive, string filePath, string destinationPathPrefix)
        {
            using (FileStream fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                ZipArchiveEntry archiveEntry = destinationAsZipArchive.CreateEntry(
                    Path.Combine(destinationPathPrefix, Path.GetFileName(filePath)));
                using (Stream archiveStream = archiveEntry.Open())
                {
                    await fileStream.CopyToAsync(archiveStream);
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
                    }
                }
            });
        }
    }
}
