using Microsoft.VisualStudio.TestTools.UnitTesting;
using wv2util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Threading;

namespace wv2util.Tests
{
    [TestClass()]
    public class ReportCreatorTests
    {
        ReportCreator CreateReportCreatorForTest()
        {
            return new ReportCreator(
                new HostAppEntry("host", "example.exe", "C:\\msedgewebview2.exe --embedded-browser-webview=1", 1, 0, "C:\\windows\\system32\\actxprxy.dll", "C:\\windows\\system32", "C:\\windows\\system32", new string[] { }, 2),
                new List<AppOverrideEntry>(),
                new List<RuntimeEntry>());
        }

        [TestMethod()]
        public void CreateReportCreator()
        {
            var result = CreateReportCreatorForTest();
        }

        [TestMethod()]
        public void DestinationIsValid()
        {
            var reportCreator = CreateReportCreatorForTest();
            string fullPathWithFileName = reportCreator.DestinationPath;
            Assert.IsFalse(File.Exists(fullPathWithFileName));

            string parentFolderPath = Path.GetDirectoryName(fullPathWithFileName);
            Assert.IsTrue(Directory.Exists(parentFolderPath));
        }

        [TestMethod()]
        public void DefaultFilesAreThere()
        {
            var reportCreator = CreateReportCreatorForTest();
            Assert.IsTrue(reportCreator.ReportFilesList.Contains(new ReportCreator.FileEntry("summary.json")));
        }

        public static bool IsZipFile(string path) 
        {
            try
            {
                using (var archive = System.IO.Compression.ZipFile.OpenRead(path))
                {
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool IsFileWithMoreThanOneByteInSize(string path)
        {
            return new FileInfo(path).Length > 1;
        }

        [TestMethod()]
        public async Task CreatingReportWorks()
        {
            var reportCreator = CreateReportCreatorForTest();
            CancellationTokenSource cts = new CancellationTokenSource();
            await reportCreator.CreateReportAsync(cts.Token);
            
            Assert.IsTrue(File.Exists(reportCreator.DestinationPath));
            Assert.IsTrue(IsFileWithMoreThanOneByteInSize(reportCreator.DestinationPath));
            Assert.IsTrue(IsZipFile(reportCreator.DestinationPath));

            // Cleanup
            File.Delete(reportCreator.DestinationPath);
        }

        [TestMethod()]
        [Ignore]
        public async Task AddDxDiagWorksAsync()
        {
            var reportCreator = CreateReportCreatorForTest();
            CancellationTokenSource cts = new CancellationTokenSource();
            await reportCreator.AddDxDiagLogAsync(cts.Token);
            var dxdiagFileEntry = reportCreator.ReportFilesList.First(
                fileEntry => fileEntry.InputPathFileName.ToLower().Contains("dxdiag"));
            Assert.IsTrue(File.Exists(dxdiagFileEntry.InputPathFileName));
            Assert.IsTrue(IsFileWithMoreThanOneByteInSize(dxdiagFileEntry.InputPathFileName));
        }

        [TestMethod()]
        [Ignore]
        public async Task AddProcMonWorksAsync()
        {
            var reportCreator = CreateReportCreatorForTest();
            CancellationTokenSource cts = new CancellationTokenSource();
            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            
            Task addLog = reportCreator.AddScenarioLogAsync(ReportCreator.LogKind.ProcMon, tcs.Task, cts.Token);

            await Task.Delay(100); // Wait 0.1 seconds

            tcs.SetResult(true);

            await addLog;
            
            bool hasFile = reportCreator.ReportFilesList.Any(
                fileEntry => fileEntry.InputPathFileName.ToLower().Contains("procmon"));
            
            Assert.IsTrue(hasFile);
        }
    }
}