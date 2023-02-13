﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;

namespace wv2util
{
    public class ReportCreator
    {
        // A file to store in the report.
        public class FileEntry : IEquatable<FileEntry>
        {
            public FileEntry(string inputPath, string outputPathFolder = "")
            {
                InputPath = inputPath;
                OutputPathFolder = outputPathFolder;
            }
            
            // The path to the file on the disk to store in the report.
            // Or just a relative name like "summary.json" for special files generated by the tool that don't exist on disk.
            public string InputPath { get; set; }
            
            // The folder within the report in which to store the file.
            public string OutputPathFolder { get; set; }

            // InputPathFileName returns just the file name part of the InputPath property
            public string InputPathFileName => Path.GetFileName(InputPath);
            
            // DestinationPath returns the DestinationPathFolder and the input path file name
            public string DestinationPath => String.IsNullOrEmpty(OutputPathFolder) ? 
                InputPathFileName :
                OutputPathFolder + "\\" + InputPathFileName;

            public bool Equals(FileEntry other) => 
                this.InputPath == other.InputPath && 
                this.OutputPathFolder == other.OutputPathFolder;

            public override string ToString() => DestinationPath;
        }

        public static string GenerateReportFileName(string hostAppExeName)
        {
            return hostAppExeName + ".WebView2Utilities.Report." + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".zip";
        }

        public string GenerateReportFileName()
        {
            return GenerateReportFileName(this.HostAppEntry.ExecutableName);
        }

        public ReportCreator(HostAppEntry hostAppEntry, IEnumerable<AppOverrideEntry> appOverrideList, IEnumerable<RuntimeEntry> runtimeList)
        {
            HostAppEntry = hostAppEntry;
            AppOverrideList = appOverrideList;
            RuntimeList = runtimeList;
            
            // Initialize the DestinationPath to a default location in the documents folder.
            this.DestinationPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\" + GenerateReportFileName();

            InitializeReportFileList();
        }

        public void InitializeReportFileList()
        {
            ReportFilesList.Add(new FileEntry("summary.json"));
            ReportFilesList.Add(new FileEntry("hostApp.json"));
            ReportFilesList.Add(new FileEntry("appOverrideList.json"));
            ReportFilesList.Add(new FileEntry("runtimeList.json"));

            if (HostAppEntry.UserDataPath != null && HostAppEntry.UserDataPath.Length > 0)
            {
                // Add crashpad dumps
                {
                    string crashpadReportFolder = Path.Combine(HostAppEntry.UserDataPath, "Crashpad", "reports");
                    // Get all the files in the crashpad report folder
                    string[] crashpadReportFiles = Directory.GetFiles(crashpadReportFolder);
                    foreach (string crashpadReportFile in crashpadReportFiles)
                    {
                        // Add the file to the zip archive
                        this.ReportFilesList.Add(new FileEntry(crashpadReportFile, "CrashpadReports"));
                    }
                }

                // Add log files
                {
                    string logFolder = HostAppEntry.UserDataPath;
                    string[] logFiles = Directory.GetFiles(logFolder, "*.log");
                    foreach (string logFile in logFiles)
                    {
                        this.ReportFilesList.Add(new FileEntry(logFile, "logs"));
                    }
                }
            }
        }

        public HostAppEntry HostAppEntry { get; private set; }
        public IEnumerable<AppOverrideEntry> AppOverrideList { get; private set; }
        public IEnumerable<RuntimeEntry> RuntimeList { get; private set; }
        public string DestinationPath { get; set; }
        public ObservableCollection<FileEntry> ReportFilesList = new ObservableCollection<FileEntry>();        

        public Task CreateReportAsync()
        {
            return CreateReportAsync(this.ReportFilesList, this.DestinationPath, this.HostAppEntry, this.AppOverrideList, this.RuntimeList);
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

        protected class SummaryInfo
        {
            public string WebView2UtilitiesVersion { get; set; }
            public string CreationDate { get; set; }
        }

        private static Task CreateReportAsync(ObservableCollection<FileEntry> fileEntries, string destinationPath, HostAppEntry hostAppEntry, IEnumerable<AppOverrideEntry> appOverrideList, IEnumerable<RuntimeEntry> runtimeList)
        {
            return Task.Run(async () =>
            {
                using (FileStream destinationAsFileStream = new FileStream(destinationPath, FileMode.Create))
                {
                    using (ZipArchive destinationAsZipArchive = new ZipArchive(destinationAsFileStream, ZipArchiveMode.Create))
                    {
                        foreach (FileEntry fileEntry in fileEntries)
                        {
                            switch (fileEntry.InputPath)
                            {
                                case "summary.json":
                                    await WriteObjectToZipArchiveEntryAsync(destinationAsZipArchive, new SummaryInfo
                                    {
                                        WebView2UtilitiesVersion = VersionUtil.GetWebView2UtilitiesVersion(),
                                        CreationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                                    }, fileEntry.InputPath);
                                    break;

                                case "hostApp.json":
                                    await WriteObjectToZipArchiveEntryAsync(destinationAsZipArchive, hostAppEntry, fileEntry.InputPath);
                                    break;
                                    
                                case "appOverrideList.json":
                                    await WriteObjectToZipArchiveEntryAsync(destinationAsZipArchive, appOverrideList, fileEntry.InputPath);
                                    break;

                                case "runtimeList.json":
                                    await WriteObjectToZipArchiveEntryAsync(destinationAsZipArchive, runtimeList, fileEntry.InputPath);
                                    break;

                                default:
                                    await WriteFileToZipArchiveEntryAsync(destinationAsZipArchive, fileEntry.InputPath, fileEntry.OutputPathFolder);
                                    break;
                            }
                        }
                    }
                }
            });
        }
    }
}
