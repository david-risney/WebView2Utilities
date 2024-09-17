using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;
using System.Windows.Markup;
using System.Xml.Linq;

namespace wv2util.Connect
{
    static public class ConnectUtil
    {
        private static string PathRemoveSuffix(string path)
        {
            return System.IO.Path.GetDirectoryName(path);
        }

        private static void CustomSchemeCommandToOptions(
            string customSchemeCommand,
            CoreWebView2EnvironmentOptions options)
        {
            // custom-scheme,0,0,wv2rocks,1,1
            string[] parts = customSchemeCommand?.Split(new char[] { ',' });
            for (int idx = 0; idx < parts?.Length; ++idx)
            {
                var registration = new CoreWebView2CustomSchemeRegistration(parts[idx]);
                registration.TreatAsSecure = int.Parse(parts[idx + 1]) == 1;
                registration.HasAuthorityComponent = int.Parse(parts[idx + 2]) == 2;
                options.CustomSchemeRegistrations.Add(registration);
            }
        }

        public static async Task<CoreWebView2Environment> CreateEnvironmentAsync(HostAppEntry runtime)
        {
            if (runtime.IntegrityLevel != "Normal")
            {
                throw new Exception("Currently Connect only works with medium IL processes. Not high or appcontainer.");
            }

            string browserExecutableFolder = PathRemoveSuffix(runtime.Runtime.ExePath);

            // Example command line:
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
            var parsedCommandLine = new CommandLineUtil.CommandLine(runtime.CommandLine);
            CoreWebView2EnvironmentOptions options = new CoreWebView2EnvironmentOptions();

            // remove all the baseline arguments from the parsedCommandLine
            parsedCommandLine.RemoveAt(0); // Remove the process name
            parsedCommandLine.RemovePrefixedCommand("--embedded-browser-webview=");
            parsedCommandLine.RemovePrefixedCommand("--webview-exe-name=");
            parsedCommandLine.RemovePrefixedCommand("--webview-exe-version=");
            parsedCommandLine.RemovePrefixedCommand("--noerrdialogs");
            parsedCommandLine.RemovePrefixedCommand("--mojo-named-platform-channel-pipe=");
            parsedCommandLine.RemovePrefixedCommand("/pfhostedapp:");

            string userDataFolder = PathRemoveSuffix(runtime.UserDataPath);
            parsedCommandLine.RemovePrefixedCommand("--user-data-dir=");

            bool enableExtensions = parsedCommandLine.Contains("--embedded-browser-webview-enable-extension");
            parsedCommandLine.RemovePrefixedCommand("--embedded-browser-webview-enable-extension");
            options.AreBrowserExtensionsEnabled = enableExtensions;

            string lang = parsedCommandLine.GetKeyValue("--lang");
            parsedCommandLine.RemovePrefixedCommand("--lang");
            if (lang != null)
            {
                options.Language = lang;
            }

            string dpiAwareness = parsedCommandLine.GetKeyValue("--embedded-browser-webview-dpi-awareness");
            // parsedCommandLine.RemovePrefixedCommand("--embedded-browser-webview-dpi-awareness=");

            string customUriSchemesStringData = parsedCommandLine.GetKeyValue("--edge-webview-custom-scheme");
            if (customUriSchemesStringData != null)
            {
                parsedCommandLine.RemovePrefixedCommand("--edge-webview-custom-scheme");
                CustomSchemeCommandToOptions(customUriSchemesStringData, options);
            }

            options.AdditionalBrowserArguments = parsedCommandLine.ToString();
            return await CoreWebView2Environment.CreateAsync(browserExecutableFolder, userDataFolder, options);
        }
    }
}
