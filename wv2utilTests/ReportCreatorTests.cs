using Microsoft.VisualStudio.TestTools.UnitTesting;
using wv2util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
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
                new HostAppEntry("example.exe", 1, "C:\\windows\\system32\\actxprxy.dll", "C:\\", "C:\\", new string[] { }, 2),
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

        [TestMethod()]
        public async Task AddDxDiagWorksAsync()
        {
            var reportCreator = CreateReportCreatorForTest();
            CancellationTokenSource cts = new CancellationTokenSource();
            await reportCreator.AddDxDiagLogAsync(cts.Token);
            bool hasFile = reportCreator.ReportFilesList.Any(
                fileEntry => fileEntry.InputPathFileName.ToLower().Contains("dxdiag"));
            Assert.IsTrue(hasFile);
        }
    }
}