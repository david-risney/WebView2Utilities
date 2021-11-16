using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace wv2util
{
    public class InstallableRuntimeEntry
    {
        public InstallableRuntimeEntry(string version, string arch, string channel, string downloadUri, string moreInfoUri)
        {
            Version = version;
            Arch = arch;
            Channel = channel;
            DownloadUri = downloadUri;
            MoreInfoUri = moreInfoUri;
        }
        public string Version { get; private set; } = "Unknown";
        public string Arch { get; private set; } = "Unknown";
        public string Channel { get; private set; } = "Unknown"; // Canary, Dev, Beta, WebView2 Runtime, Fixed Version
        public string DownloadUri { get; private set; } = "https://example.com/";
        public string MoreInfoUri { get; private set; } = "https://example.com/";
    }

    public class InstallableRuntimeList : ObservableCollection<InstallableRuntimeEntry>
    {
        public InstallableRuntimeList()
        {
            InitializeHttp();
            _ = FromAsync();
        }

        private Task m_inProgressFrom = null;

        // This is clearly not thread safe. It assumes FromAsync will only
        // be called from the same thread.
        public async Task FromAsync()
        {
            if (m_inProgressFrom != null)
            {
                await m_inProgressFrom;
            }
            else
            {
                m_inProgressFrom = FromInnerAsync();
                await m_inProgressFrom;
                m_inProgressFrom = null;
            }
        }

        private async Task FromInnerAsync()
        {
            IEnumerable<InstallableRuntimeEntry> newEntries = null;
            await Task.Run(async () => {
                newEntries = await GetEntries();
            });
            // Only update the entries on the caller thread to ensure the
            // caller isn't trying to enumerate the entries while
            // we're updating them.
            this.SetEntries(newEntries);
        }

        protected void SetEntries(IEnumerable<InstallableRuntimeEntry> newEntries)
        {
            // Use ToList to get a fixed collection that won't get angry that we're calling
            // Add and Remove on it while enumerating.
            this.Items.Clear();
            foreach (var entry in newEntries)
            {
                this.Items.Add(entry);
            }

            this.OnPropertyChanged(new PropertyChangedEventArgs("Count"));
            this.OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private static async Task<IEnumerable<InstallableRuntimeEntry>> GetEntries()
        {
            List<InstallableRuntimeEntry> results = new List<InstallableRuntimeEntry>();
            IEnumerable<InstallableRuntimeEntry> entries1 = await GetFixedVersionEntries();
            IEnumerable<InstallableRuntimeEntry> entries2 = await GetBrowserEntries();
            results.AddRange(entries1);
            results.AddRange(entries2);
            return results;
        }

        private static HttpClient m_httpClient = null;
        private static void InitializeHttp()
        {
            if (m_httpClient == null)
            {
                m_httpClient = new HttpClient(
                    new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate });
                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                m_httpClient.Timeout = new TimeSpan(0, 1, 30);
            }
        }

        private static async Task<string> ResolveUriToStringAsync(string uri)
        {
            string response = null;

            try
            {
                response = await m_httpClient.GetStringAsync(uri);
            }
            catch (Exception)
            {
                Console.WriteLine("Failed to get webview2 fixed version URI info");
            }
            return response;
        }

        private static async Task<IEnumerable<InstallableRuntimeEntry>> GetFixedVersionEntries()
        {
            // https://edgeupdates.microsoft.com/api/products/webview2fixed?view=developer
            List<InstallableRuntimeEntry> results = null;
            string response = await ResolveUriToStringAsync("https://edgeupdates.microsoft.com/api/products/webview2fixed?view=developer");

            // Create the list after the last await... just in case
            results = new List<InstallableRuntimeEntry>();
            dynamic responseAsJson = JsonConvert.DeserializeObject(response);
            foreach (dynamic entryJson in responseAsJson[0].Releases)
            {
                try
                {
                    string version = entryJson.ProductVersion;
                    string arch = entryJson.Architecture;
                    string uri = entryJson.Artifacts[0].Location;
                    results.Add(new InstallableRuntimeEntry(version, arch, "Fixed Version", uri, "https://developer.microsoft.com/en-us/microsoft-edge/webview2/#download-section"));
                }
                catch (Exception)
                {
                    Console.WriteLine("Failure to parse webview2 fixed version json");
                }
            }
            return results;
        }

        private static async Task<IEnumerable<InstallableRuntimeEntry>> GetBrowserEntries()
        {
            // https://edgeupdates.microsoft.com/api/products?view=enterprise
            List<InstallableRuntimeEntry> results = null;
            string response = await ResolveUriToStringAsync("https://edgeupdates.microsoft.com/api/products?view=enterprise");

            // Create the list after the last await... just in case
            results = new List<InstallableRuntimeEntry>();
            dynamic responseAsJson = JsonConvert.DeserializeObject(response);
            foreach (dynamic entryJson in responseAsJson)
            {
                try
                {
                    if (entryJson.Product == "Dev" || entryJson.Product == "Beta")
                    {
                        string channel = entryJson.Product;
                        foreach (dynamic release in entryJson.Releases)
                        {
                            if (release.Platform == "Windows")
                            {
                                string version = entryJson.ProductVersion;
                                string arch = release.Architecture;
                                string uri = release.Artifacts[0].Location;
                                results.Add(new InstallableRuntimeEntry(version, arch, channel, uri, "https://www.microsoftedgeinsider.com/en-us/download/"));
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Skipping an entry in browser json " + e);
                }
            }
            return results;
        }
    }
}
