﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace wv2util
{
    public class ReportCreator
    {
        // A file to store in the report.
        public class FileEntry : IEquatable<FileEntry>
        {
            public FileEntry(string inputPath, string outputPathFolder = "", bool deleteWhenDone = false)
            {
                InputPath = inputPath;
                OutputPathFolder = outputPathFolder;
            }
            
            // The path to the file on the disk to store in the report.
            // Or just a relative name like "summary.json" for special files generated by the tool that don't exist on disk.
            public string InputPath { get; set; }
            
            // The folder within the report in which to store the file.
            public string OutputPathFolder { get; set; }

            // After successfully writing the report or cancelling the report creation, delete the file
            public bool TemporaryFile { get; set; }

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

        public static string GenerateReportFileName(string hostAppExeName, string filePart = "Report", string fileType = "zip")
        {
            return hostAppExeName + ".WebView2Utilities." + filePart + "." + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + "." + fileType;
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

        private void AddPathWithEnvironmentVariableIfItExists(ObservableCollection<FileEntry> fileEntries, string pathWithEnvironmentVariable, string outputFolder)
        {
            string path = Environment.ExpandEnvironmentVariables(pathWithEnvironmentVariable);
            if (File.Exists(path))
            {
                fileEntries.Add(new FileEntry(path, outputFolder));
            }            
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
                    if (Directory.Exists(crashpadReportFolder))
                    {
                        string[] crashpadReportFiles = Directory.GetFiles(crashpadReportFolder);
                        foreach (string crashpadReportFile in crashpadReportFiles)
                        {
                            // Add the file to the zip archive
                            this.ReportFilesList.Add(new FileEntry(crashpadReportFile, "CrashpadReports"));
                        }
                    }
                }

                // Add log files
                {
                    string logFolder = HostAppEntry.UserDataPath;
                    if (Directory.Exists(logFolder))
                    {
                        string[] logFiles = Directory.GetFiles(logFolder, "*.log");
                        foreach (string logFile in logFiles)
                        {
                            this.ReportFilesList.Add(new FileEntry(logFile, "logs"));
                        }
                    }
                }
            }

            AddPathWithEnvironmentVariableIfItExists(this.ReportFilesList, "%temp%\\msedge_installer.log", "logs\\install\\temp");
            AddPathWithEnvironmentVariableIfItExists(this.ReportFilesList, "%systemroot%\\Temp\\msedge_installer.log", "logs\\install\\systemroot");
            AddPathWithEnvironmentVariableIfItExists(this.ReportFilesList, "%localappdata%\\Temp\\msedge_installer.log", "logs\\install\\localappdata");

            AddPathWithEnvironmentVariableIfItExists(this.ReportFilesList, "%localappdata%\\Temp\\MicrosoftEdgeUpdate.log", "logs\\install\\localappdata");
            AddPathWithEnvironmentVariableIfItExists(this.ReportFilesList, "%ALLUSERSPROFILE%\\Microsoft\\EdgeUpdate\\Log\\MicrosoftEdgeUpdate.log", "logs\\install\\allusersprofile");
            AddPathWithEnvironmentVariableIfItExists(this.ReportFilesList, "%ProgramData%\\Microsoft\\EdgeUpdate\\Log\\MicrosoftEdgeUpdate.log", "logs\\install\\programdata");
        }

        public void Cleanup()
        {
            foreach (var entry in this.ReportFilesList)
            {
                if (entry.TemporaryFile)
                {
                    try
                    {
                        File.Delete(entry.InputPathFileName);
                    }
                    catch (Exception e)
                    {
                        // Ignore failure for temporary file deletion. We try but if it doesn't work, we move on.
                        Console.WriteLine("Failed to delete temporary file " + entry.InputPathFileName + ": " + e.Message);
                    }
                }
            }
        }

        public HostAppEntry HostAppEntry { get; private set; }
        public IEnumerable<AppOverrideEntry> AppOverrideList { get; private set; }
        public IEnumerable<RuntimeEntry> RuntimeList { get; private set; }
        public string DestinationPath { get; set; }
        public ObservableCollection<FileEntry> ReportFilesList = new ObservableCollection<FileEntry>();        

        public Task CreateReportAsync(CancellationToken cancellationToken)
        {
            return CreateReportAsync(this.ReportFilesList, this.DestinationPath, this.HostAppEntry, this.AppOverrideList, this.RuntimeList, cancellationToken);
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

        private static Task CreateReportAsync(
            ObservableCollection<FileEntry> fileEntries,
            string destinationPath,
            HostAppEntry hostAppEntry,
            IEnumerable<AppOverrideEntry> appOverrideList,
            IEnumerable<RuntimeEntry> runtimeList,
            CancellationToken token)
        {
            return Task.Run(async () =>
            {
                using (FileStream destinationAsFileStream = new FileStream(destinationPath, FileMode.Create))
                {
                    token.Register(() =>
                    {
                        try
                        {
                            destinationAsFileStream.Dispose();
                            destinationAsFileStream.Close();
                            File.Delete(destinationPath);
                        }
                        catch (Exception e)
                        {
                            // Clean up is best effort
                            Console.WriteLine("Failed to close and delete report file: " + e.Message);
                        }                        
                    });
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

        public async Task AddDxDiagLogAsync(CancellationToken cancellationToken)
        {
            string dxDiagLogPath = Environment.ExpandEnvironmentVariables("%TEMP%") + 
                "\\" + 
                GenerateReportFileName(this.HostAppEntry.ExecutableName, "DxDiagLog", "xml");
            
            await CreateDxDiagLogAsync(dxDiagLogPath, cancellationToken);
            if (!File.Exists(dxDiagLogPath))
            {
                throw new FileNotFoundException("Failed to create DxDiag log file", dxDiagLogPath);
            }
            this.ReportFilesList.Add(new FileEntry(dxDiagLogPath, "logs", true));
        }

        private static async Task CreateDxDiagLogAsync(string outputPath, CancellationToken cancellationToken)
        {
            System.Diagnostics.Process dxDiagProcess = new System.Diagnostics.Process();
            dxDiagProcess.StartInfo.FileName = "dxdiag";
            dxDiagProcess.StartInfo.Arguments = @"/whql:off /dontskip /t /x " + outputPath;
            dxDiagProcess.StartInfo.UseShellExecute = false;
            dxDiagProcess.StartInfo.RedirectStandardOutput = false;

            dxDiagProcess.Start();

            cancellationToken.Register(() =>
            {
                try
                {
                    dxDiagProcess.Kill();
                }
                catch (Exception e)
                {
                    // Ignore failure to kill process. We try but if it doesn't work, we move on.
                    Console.WriteLine("Failed to kill DxDiag process: " + e.Message);
                }
            });

            await WaitForProcessAsync(dxDiagProcess, cancellationToken);
        }

        private static Task WaitForProcessAsync(
            System.Diagnostics.Process process, 
            CancellationToken cancellationToken, 
            int timeoutInMilliseconds = 60000,
            bool checkExitCode = false)
        {
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            ThreadPool.QueueUserWorkItem((object state) =>
            {
                bool exited = process.WaitForExit(timeoutInMilliseconds);
                if (cancellationToken.IsCancellationRequested)
                {
                    tcs.SetCanceled();
                }
                else if (!exited)
                {
                    tcs.SetException(new Exception("Process is taking too long to exit."));
                    process.Kill();
                }
                else if (checkExitCode && process.ExitCode != 0)
                {
                    tcs.SetException(new Exception("Process exited with error code " + process.ExitCode));
                }
                else
                {
                    tcs.SetResult(true);
                }
            });
            return tcs.Task;
        }
    }
}
