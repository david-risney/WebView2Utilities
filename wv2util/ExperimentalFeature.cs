using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;

namespace wv2util
{
    public class ExperimentalFeature : IEquatable<ExperimentalFeature>, IComparable<ExperimentalFeature>, INotifyPropertyChanged
    {
        public string Name { get; set; }
        
        private bool m_IsEnabled;

        public bool IsEnabled
        {
            get { return m_IsEnabled; }
            set
            {
                if (value != m_IsEnabled)
                {
                    if (value)
                    {
                        bool previousIsEnabled = m_IsEnabled;
                        m_IsEnabled = m_turnOn();
                        if (previousIsEnabled != m_IsEnabled)
                        {
                            OnPropertyChanged("IsEnabled");
                        }
                    }
                    else
                    {
                        m_turnOff();
                        m_IsEnabled = false;
                        OnPropertyChanged("IsEnabled");
                    }
                }
            }
        }

        public string Description { get; set; }

        private Func<bool> m_turnOn;
        private Action m_turnOff;
        private Func<bool> m_isOn;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        protected void QueryEnabled()
        {
            bool previousEnabledValue = m_IsEnabled;
            m_IsEnabled = m_isOn();
            if (previousEnabledValue != m_IsEnabled)
            {
                OnPropertyChanged("IsEnabled");
            }
        }

        public ExperimentalFeature(Func<bool> turnOn, Action turnOff, Func<bool> isOn)
        {
            this.m_turnOn = turnOn;
            this.m_turnOff = turnOff;
            this.m_isOn = isOn;
            QueryEnabled();
        }

        public int CompareTo(ExperimentalFeature other)
        {
            return Name.CompareTo(other.Name);
        }

        public bool Equals(ExperimentalFeature other)
        {
            return Name.Equals(other.Name);
        }
    }

    public class EnvVarExperimentalFeature : ExperimentalFeature
    {
        public EnvVarExperimentalFeature(string envVar, string onVal, string offVal) : base(
            () =>
            {
                // Turn on:
                Environment.SetEnvironmentVariable(envVar, onVal, EnvironmentVariableTarget.User);
                return true;
            },
            () =>
            {
                // Turn off:
                Environment.SetEnvironmentVariable(envVar, offVal, EnvironmentVariableTarget.User);
                Environment.SetEnvironmentVariable(envVar, offVal, EnvironmentVariableTarget.Machine);
            },
            () =>
            {
                string val = Environment.GetEnvironmentVariable(envVar, EnvironmentVariableTarget.User);
                return val != null && val == onVal;
            })
        {
        }
    }
    
    public class CanaryPreviewExperimentalFeature : ExperimentalFeature
    {
        public CanaryPreviewExperimentalFeature() : base(
                () =>
                {
                    var runtimes = AppState.GetRuntimeList();

                    // Only if Canary is installed turn self-hosting on
                    if (runtimes != null && runtimes.Any(runtime => runtime.Channel == "Canary"))
                    {
                        var appOverrideList = AppState.GetAppOverrideList();
                        var overrideCandidateList = appOverrideList.Where(entry => entry.HostApp == "*").ToList();
                        // We find all override entries that apply to all apps '*'
                        // and ensure the highest precedent one performs canary
                        // selfhost.
                        // Sorting by StorageKind gives the highest precedent override.
                        overrideCandidateList.Sort((left, right) => left.StorageKind - right.StorageKind);
                        if (overrideCandidateList.Count == 0)
                        {
                            Debug.WriteLine("When turning on selfhost we couldn't find any override candidates. There should always be at least the HKCU * override.");
                        }
                        else
                        {
                            var entry = overrideCandidateList[0];
                            entry.IsRuntimeEvergreenPreview = true;
                            entry.ReverseSearchOrder = true;
                            entry.ReleaseChannels =
                                AppOverrideEntry.Channels.Canary |
                                AppOverrideEntry.Channels.Dev |
                                AppOverrideEntry.Channels.Beta |
                                AppOverrideEntry.Channels.Stable;
                        }

                        return true;
                    }
                    else
                    {
                        const string canaryLink = "https://go.microsoft.com/fwlink/?linkid=2084649&Channel=Canary&language=en";
                        if (MessageBox.Show(
                            $"Before turning on this feature you need to have Canary installed from {canaryLink}. Do you want to install it now?",
                            "Canary missing", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                        {
                            System.Diagnostics.Process.Start(canaryLink);
                        }

                        return false;
                    }
                },
                () =>
                {
                    // Turn off:
                    // We will find all override entries that apply to all apps ('*') and
                    // make sure they don't try to force canary.
                    var appOverrideList = AppState.GetAppOverrideList();
                    if (appOverrideList != null)
                    {
                        // Remove the entries outside the foreach so nothing gets upset
                        // about us enumerating a list and modifying it at the same time.
                        List<AppOverrideEntry> entriesToRemove = new List<AppOverrideEntry>();

                        foreach (var entry in appOverrideList)
                        {
                            if (entry.HostApp == "*")
                            {
                                // Override entries could try to force canary by reversing the search
                                // order to find canary first
                                // or by limiting the channel selection to only canary.
                                // So we replace both of these to make sure they are the usual default
                                // value.
                                entry.ReleaseChannels =
                                    AppOverrideEntry.Channels.Beta |
                                    AppOverrideEntry.Channels.Canary |
                                    AppOverrideEntry.Channels.Dev |
                                    AppOverrideEntry.Channels.Stable;
                                entry.ReverseSearchOrder = false;
                                entry.IsRuntimeEvergreen = true;

                                // If the entry doesn't do anything else, doesn't change
                                // the user data folder and doesn't change the browser
                                // arguments then we its a no-op entry and we can just remove
                                // it (except for HKCU which we always keep around).
                                if (entry.StorageKind != StorageKind.HKCU &&
                                    String.IsNullOrEmpty(entry.UserDataPath) &&
                                    String.IsNullOrEmpty(entry.BrowserArguments))
                                {
                                    entriesToRemove.Add(entry);
                                }
                            }
                        }

                        foreach (var entryToRemove in entriesToRemove)
                        {
                            appOverrideList.Remove(entryToRemove);
                        }
                    }
                },
                () =>
                {
                    var runtimes = AppState.GetRuntimeList();
                    // We only consider self hosting on if Canary is installed and
                    // the highest precedent * override includes canary in the release channels
                    // and reverses the search order.
                    if (runtimes != null && runtimes.Any(runtime => runtime.Channel == "Canary"))
                    {
                        var appOverrideList = AppState.GetAppOverrideList();
                        if (appOverrideList != null)
                        {
                            var overrideCandidateList = appOverrideList.Where(entry => entry.HostApp == "*").ToList();
                            // We find all override entries that apply to all apps '*'
                            // and ensure the highest precedent one performs canary
                            // selfhost.
                            // Sorting by StorageKind gives the highest precedent override.
                            overrideCandidateList.Sort((left, right) => left.StorageKind - right.StorageKind);
                            if (overrideCandidateList.Count == 0)
                            {
                                Debug.WriteLine("When turning on selfhost we couldn't find any override candidates. There should always be at least the HKCU * override.");
                            }
                            else
                            {
                                // We are selfhosting if either we're reversing search order and canary is included 
                                // or we're not reversing search order but only canary is included.
                                var entry = overrideCandidateList[0];
                                return
                                    entry.IsRuntimeEvergreenPreview && (
                                        (entry.ReleaseChannels == AppOverrideEntry.Channels.Canary) ||
                                        (entry.ReverseSearchOrder && (entry.ReleaseChannels & AppOverrideEntry.Channels.Canary) != 0));
                            }
                        }
                    }
                    return false;
                })
        {
            Name = "Preview WebView2 Runtime";
            Description = "Host apps use canary WebView2 Runtime if installed instead of stable.";

            AppState.GetRuntimeList().CollectionChanged += DependencyListChanged;
            AppState.GetAppOverrideList().CollectionChanged += DependencyListChanged;
        }

        private void DependencyListChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            SubscribeToOverrides();
            QueryEnabled();
        }

        private void SubscribeToOverrides()
        {
            foreach (var entry in AppState.GetAppOverrideList()?.Where(entry => entry.HostApp == "*"))
            {
                entry.PropertyChanged += DependencyOverrideEntryChanged;
            }
        }

        private void DependencyOverrideEntryChanged(object sender, PropertyChangedEventArgs e)
        {
            QueryEnabled();
        }
    }

    public class ExperimentalFeatureList : ObservableCollection<ExperimentalFeature>
    {
        public ExperimentalFeatureList()
        {
            // Canary self-hosting
            Items.Add(new CanaryPreviewExperimentalFeature());

            // Visual Hosting
            Items.Add(new EnvVarExperimentalFeature(
                "COREWEBVIEW2_FORCED_HOSTING_MODE",
                "COREWEBVIEW2_HOSTING_MODE_WINDOW_TO_VISUAL",
                null)
            {
                Name = "Force window to visual hosting",
                Description = "Host apps use visual hosting instead of window hosting."
            });

            // To add more experimental features to the runtimes either:
            // add EnvVarExperimentalFeature if the feature is controlled only by an enviroment variable
            // OR
            // add ExperimentalFeature with on, off and check delegates if the feature requires more specific operations
        }
    }
}