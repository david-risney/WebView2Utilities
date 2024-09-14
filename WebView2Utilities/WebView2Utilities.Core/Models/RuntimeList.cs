using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WebView2Utilities.Core.Models;

public class RuntimeEntry : IEquatable<RuntimeEntry>, IComparable<RuntimeEntry>
{
    // Create with the path to msedgewebview2.exe of the
    // corresponding WebView2 Runtime.
    public RuntimeEntry(string webview2RuntimeExePath)
    {
        ExePath = webview2RuntimeExePath;
    }
    public string ExePath
    {
        get; protected set;
    }
    public string RuntimeLocation => ExePath != "" && ExePath != null ? Directory.GetParent(ExePath).FullName : "Unknown";
    public string Version => VersionUtil.GetVersionStringFromFilePath(ExePath);
    public string Channel
    {
        get
        {
            if (!string.IsNullOrEmpty(ExePath))
            {
                var exePathLower = ExePath.ToLower();
                if (exePathLower.Contains("\\edge sxs\\"))
                {
                    return "Canary";
                }
                else if (exePathLower.Contains("\\edge beta\\"))
                {
                    return "Beta";
                }
                else if (exePathLower.Contains("\\edge dev\\"))
                {
                    return "Dev";
                }
                else if (exePathLower.Contains("\\edge\\"))
                {
                    return "Stable";
                }
                else if (exePathLower.Contains("\\edgewebview\\"))
                {
                    return "Stable WebView2 Runtime";
                }
            }
            return "Unknown";
        }
    }

    public bool Equals(RuntimeEntry other) => CompareTo(other) == 0;

    // The default comparison for a RuntimeEntry is by channel (most stable first) then by version (newest first).
    // And last sorted by the RuntimeLocation which determines equality.
    public int CompareTo(RuntimeEntry other)
    {
        var comparison = -SortUtil.CompareChannelStrings(Channel, other.Channel);
        if (comparison == 0)
        {
            comparison = -SortUtil.CompareVersionStrings(Version, other.Version);
            if (comparison == 0)
            {
                comparison = RuntimeLocation.ToLower().CompareTo(other.RuntimeLocation.ToLower());
            }
        }
        return comparison;
    }

    public static Comparison<RuntimeEntry> Comparison = (left, right) => left.CompareTo(right);

}

public class RuntimeList : ObservableCollection<RuntimeEntry>
{
    public RuntimeList()
    {
        _ = FromDiskAsync();
    }

    private Task m_inProgressFromDisk = null;

    // This is clearly not thread safe. It assumes FromDiskAsync will only
    // be called from the same thread.
    public async Task FromDiskAsync()
    {
        if (m_inProgressFromDisk != null)
        {
            await m_inProgressFromDisk;
        }
        else
        {
            m_inProgressFromDisk = FromDiskInnerAsync();
            await m_inProgressFromDisk;
            m_inProgressFromDisk = null;

            // Use default sort
            Sort(RuntimeEntry.Comparison);
        }
    }

    private async Task FromDiskInnerAsync()
    {
        IEnumerable<RuntimeEntry> newEntries = null;
        await Task.Factory.StartNew(() =>
        {
            newEntries = GetRuntimes();
        });
        // Only update the entries on the caller thread to ensure the
        // caller isn't trying to enumerate the entries while
        // we're updating them.
        SetEntries(newEntries);
    }

    protected void SetEntries(IEnumerable<RuntimeEntry> newEntries)
    {
        // Use ToList to get a fixed collection that won't get angry that we're calling
        // Add and Remove on it while enumerating.
        foreach (var entry in this.Except(newEntries).ToList())
        {
            Items.Remove(entry);
        }
        foreach (var entry in newEntries.Except(this).ToList())
        {
            Items.Add(entry);
        }

        OnPropertyChanged(new PropertyChangedEventArgs("Count"));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public void Sort<T>(Comparison<T> comparison)
    {
        ArrayList.Adapter((IList)Items).Sort(new SortUtil.ComparisonComparer<T>(comparison));

        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    private static List<RuntimeEntry> GetLocalRepoRuntimesInRoot(DirectoryInfo root)
    {
        var runtimes = new List<RuntimeEntry>();
        try
        {
            foreach (var subDirectory in root.GetDirectories())
            {
                try
                {
                    var srcDirectory = subDirectory.GetDirectories("src").FirstOrDefault();
                    if (srcDirectory != null)
                    {
                        try
                        {
                            var outDirectory = srcDirectory.GetDirectories("out").FirstOrDefault();
                            if (outDirectory != null)
                            {
                                foreach (var buildDirectory in outDirectory.GetDirectories("release_*"))
                                {
                                    try
                                    {
                                        var exeFile = buildDirectory.GetFiles("msedgewebview2.exe").FirstOrDefault();
                                        if (exeFile != null)
                                        {
                                            runtimes.Add(new RuntimeEntry(exeFile.FullName));
                                        }
                                    }
                                    catch (Exception) { }
                                }
                            }
                        }
                        catch (Exception) { }
                    }
                }
                catch (Exception) { }
            }
        }
        catch (Exception) { }
        return runtimes;
    }

    private static IEnumerable<RuntimeEntry> GetLocalRepoRuntimes()
    {
        IEnumerable<RuntimeEntry> runtimes = new List<RuntimeEntry>();
        foreach (var driveInfo in DriveInfo.GetDrives().Where(
            drive => drive.IsReady && // Try to skip CDs or other removable media that's not ready
            drive.TotalSize > 1073741824)) // Skip disks too small to contain local repos
        {
            runtimes = runtimes.Concat(GetLocalRepoRuntimesInRoot(driveInfo.RootDirectory));
            foreach (var rootFolder in driveInfo.RootDirectory.GetDirectories())
            {
                try
                {
                    runtimes = runtimes.Concat(GetLocalRepoRuntimesInRoot(rootFolder));
                }
                catch (Exception) { }
            }
        }
        return runtimes;
    }

    private static IEnumerable<RuntimeEntry> GetDownloadFolderRuntimes()
    {
        DirectoryInfo downloadFolder = null;

        try
        {
            downloadFolder = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)).GetDirectories("Downloads").FirstOrDefault();
        }
        catch (Exception) { }

        if (downloadFolder != null)
        {
            foreach (var subFolder in downloadFolder.GetDirectories())
            {
                var exeFile = subFolder.GetFiles("msedgewebview2.exe").FirstOrDefault();
                if (exeFile != null)
                {
                    yield return new RuntimeEntry(exeFile.FullName);
                }
            }
        }
    }

    private static IEnumerable<RuntimeEntry> GetInstalledRuntimes()
    {
        var potentialParents = new List<string>();
        foreach (var rootPath in new string[] {
            Environment.GetEnvironmentVariable("LOCALAPPDATA") + "\\Microsoft\\",
            Environment.GetEnvironmentVariable("ProgramFiles(x86)") + "\\Microsoft\\",
            Environment.GetEnvironmentVariable("ProgramFiles") + "\\Microsoft\\"
        })
        {
            try
            {
                potentialParents.AddRange(Directory.GetDirectories(rootPath).Where(path => path.Contains("Edge")));
            }
            catch (DirectoryNotFoundException e)
            {
                Debug.WriteLine("Ignoring DirectoryNotFoundException exception while searching for WebView2 runtimes: " + e.Message);
            }
        }

        foreach (var potentialParent in potentialParents)
        {
            string[] foundExes = null;
            try
            {
                foundExes = Directory.GetFiles(potentialParent, "msedgewebview2.exe", SearchOption.AllDirectories);
            }
            catch (UnauthorizedAccessException e)
            {
                Debug.WriteLine("Ignoring unauthorized access exception while searching for WebView2 runtimes: " + e.Message);
            }

            foreach (var path in foundExes)
            {
                if (!path.ToLower().Contains(@"edge\application") && !path.ToLower().Contains("edgecore"))
                {
                    yield return new RuntimeEntry(path);
                }
            }
        }
    }

    private static IEnumerable<RuntimeEntry> GetRuntimes()
    {
        return GetInstalledRuntimes().Concat(
            GetLocalRepoRuntimes()).Concat(
            GetDownloadFolderRuntimes()).ToHashSet();
    }
}
