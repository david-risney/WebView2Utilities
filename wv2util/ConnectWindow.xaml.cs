using Markdig.Extensions.Tables;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace wv2util
{
    /// <summary>
    /// Interaction logic for ConnectWindow.xaml
    /// </summary>
    public partial class ConnectWindow : Window
    {
        private HostAppEntry m_hostAppEntry;
        private HostAppEntry m_runtimeEntry;

        public ConnectWindow(HostAppEntry hostAppEntry, HostAppEntry runtimeEntry)
        {
            m_hostAppEntry = hostAppEntry;
            m_runtimeEntry = runtimeEntry;

            InitializeComponent();

            _ = InitializeWebView2();
        }

        private static string PathRemoveSuffix(string path)
        {
            return System.IO.Path.GetDirectoryName(path);
        }

        private async Task<Tuple<CoreWebView2Environment, CoreWebView2ControllerOptions>> HostAppEntryToCoreWebView2CreationOptionsAsync()
        {
            string udf = PathRemoveSuffix(m_runtimeEntry.UserDataPath);
            var parsedCommandLine = new CommandLineUtil.CommandLine(m_runtimeEntry.CommandLine);
            /*
--embedded-browser-webview=1 
--webview-exe-name=WebView2APISample.exe 
--user-data-dir="C:\Users\dave\source\repos\WebView2Samples\SampleApps\WebView2APISample\Debug\x64\WebView2APISample.exe.WebView2\EBWebView" 
--noerrdialogs 
--embedded-browser-webview-dpi-awareness=2 
--edge-webview-custom-scheme=custom-scheme,0,0,wv2rocks,1,1 
--embedded-browser-webview-enable-extension 
--enable-features=MojoIpcz,ThirdPartyStoragePartitioning,PartitionedCookies 
--mojo-named-platform-channel-pipe=17404.68.8655671596765438503 
/pfhostedapp:2223bd5b94416e34fd8aad336dbb6a512b5fee31
            */

            // remove all the baseline arguments from the parsedCommandLine
            parsedCommandLine.RemovePrefixedCommand("--embedded-browser-webview=");
            parsedCommandLine.RemovePrefixedCommand("--webview-exe-name=");
            parsedCommandLine.RemovePrefixedCommand("--webview-exe-version=");
            parsedCommandLine.RemovePrefixedCommand("--user-data-dir=");
            parsedCommandLine.RemovePrefixedCommand("--noerrdialogs");
            parsedCommandLine.RemovePrefixedCommand("--embedded-browser-webview-enable-extension");
            parsedCommandLine.RemovePrefixedCommand("--mojo-named-platform-channel-pipe=");
            parsedCommandLine.RemovePrefixedCommand("/pfhostedapp:");
            // parsedCommandLine.RemovePrefixedCommand("--embedded-browser-webview-dpi-awareness");
            parsedCommandLine.RemoveAt(0); // Remove the process name
            string[] customUriSchemesStringData = parsedCommandLine.GetKeyValue("--edge-webview-custom-scheme")?.Split(',');
            // parsedCommandLine.RemovePrefixedCommand("--edge-webview-custom-scheme");

            var parsedCommandLineAsString = parsedCommandLine.ToString();
            var environmentOptions = new CoreWebView2EnvironmentOptions(parsedCommandLineAsString);

            for (int idx = 0; idx < customUriSchemesStringData?.Length; idx += 3)
            {
                string uriName = customUriSchemesStringData[idx];
                CoreWebView2CustomSchemeRegistration schemeRegistration = new CoreWebView2CustomSchemeRegistration(uriName);
                schemeRegistration.TreatAsSecure = int.Parse(customUriSchemesStringData[idx + 1]) == 1;
                schemeRegistration.HasAuthorityComponent = int.Parse(customUriSchemesStringData[idx + 2]) == 1;
                environmentOptions.CustomSchemeRegistrations.Add(schemeRegistration);
            }

            var environment = await CoreWebView2Environment.CreateAsync(m_runtimeEntry.ExecutablePathDirectory, udf, environmentOptions);
            var controllerOptions = environment.CreateCoreWebView2ControllerOptions();
            return new Tuple<CoreWebView2Environment, CoreWebView2ControllerOptions>(environment, controllerOptions);
        }

        public async Task InitializeWebView2()
        {
            var creationOptions = await HostAppEntryToCoreWebView2CreationOptionsAsync();
            await this.WebView.EnsureCoreWebView2Async(creationOptions.Item1, creationOptions.Item2);

            Debug.WriteLine(
                this.WebView.CoreWebView2.Environment.BrowserVersionString + "\n" + 
                this.WebView.CoreWebView2.Environment.UserDataFolder + "\n" + 
                this.WebView.CoreWebView2.BrowserProcessId);
            // Only set the binding after initializing the webview2.
            this.WebView.SetBinding(WebView2.SourceProperty, new Binding("Text") { ElementName = "UriTextBox" });
        }

        private void WebView_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            Debug.WriteLine("ConnectWindow WebView2 initialized");
        }
    }
}
