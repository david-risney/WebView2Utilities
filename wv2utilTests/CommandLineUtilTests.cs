using Microsoft.VisualStudio.TestTools.UnitTesting;
using wv2util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace wv2util.Tests
{
    [TestClass()]
    public class CommandLineUtilTests
    {
        [TestMethod()]
        public void ParseCommandLineRealWorldTest()
        {
            var results = (new CommandLineUtil.CommandLine("\"C:\\Program Files (x86)\\Microsoft\\EdgeWebView\\Application\\105.0.1343.42\\msedgewebview2.exe\" --embedded-browser-webview=1 --webview-exe-name=WebViewHost.exe --webview-exe-version=17.3.34-main --user-data-dir=\"C:\\Users\\dave\\AppData\\Local\\Temp\\VSWebView2Cache\\e8b5e6f0-42d7-422d-bc0c-f775d2716453\\EBWebView\" --noerrdialogs --embedded-browser-webview-dpi-awareness=2 --allow-file-access-from-files --mojo-named-platform-channel-pipe=40932.32536.5816090101267860823")).Parts;

            int idx = 0;
            Assert.AreEqual(results[idx++], "C:\\Program Files (x86)\\Microsoft\\EdgeWebView\\Application\\105.0.1343.42\\msedgewebview2.exe");
            Assert.AreEqual(results[idx++], "--embedded-browser-webview=1");
            Assert.AreEqual(results[idx++], "--webview-exe-name=WebViewHost.exe");
            Assert.AreEqual(results[idx++], "--webview-exe-version=17.3.34-main");
            Assert.AreEqual(results[idx++], "--user-data-dir=C:\\Users\\dave\\AppData\\Local\\Temp\\VSWebView2Cache\\e8b5e6f0-42d7-422d-bc0c-f775d2716453\\EBWebView");
            Assert.AreEqual(results[idx++], "--noerrdialogs");
            Assert.AreEqual(results[idx++], "--embedded-browser-webview-dpi-awareness=2");
            Assert.AreEqual(results[idx++], "--allow-file-access-from-files");
            Assert.AreEqual(results[idx++], "--mojo-named-platform-channel-pipe=40932.32536.5816090101267860823");              
        }

        [TestMethod()]
        public void ParseCommandLineUserDataDirWithSpaceTest()
        {
            var results = (new CommandLineUtil.CommandLine("msedgewebview2.exe --user-data-dir=\"C:\\Users\\dave exa\\mple\\example\\EBWebView\"")).Parts;

            int idx = 0;
            Assert.AreEqual(results[idx++], "msedgewebview2.exe");
            Assert.AreEqual(results[idx++], "--user-data-dir=C:\\Users\\dave exa\\mple\\example\\EBWebView");
        }
    }
}