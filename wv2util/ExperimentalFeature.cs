using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace wv2util
{
    public class ExperimentalFeature : IEquatable<ExperimentalFeature>, IComparable<ExperimentalFeature>
    {
        public string Name { get; set; }
        
        private bool m_IsEnabled;

        public bool IsEnabled
        {
            get { return m_IsEnabled; }
            set
            {
                if (value)
                {
                    m_IsEnabled = m_turnOn();
                    return;
                }
                else
                {
                    m_turnOff();
                }
                m_IsEnabled = value;
            }
        }

        public string Description { get; set; }

        private Func<bool> m_turnOn;
        private Action m_turnOff;

        public ExperimentalFeature(Func<bool> turnOn, Action turnOff, Func<bool> isOn)
        {
            this.m_turnOn = turnOn;
            this.m_turnOff = turnOff;
            m_IsEnabled = isOn();
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

    public class ExperimentalFeatureList : ObservableCollection<ExperimentalFeature>
    {
        public ExperimentalFeatureList()
        {
            // Canary self-hosting
            Items.Add(new ExperimentalFeature(
                () =>
                {
                    var runtimes = AppState.GetRuntimeList();

                    // Only if Canary is installed turn self-hosting on
                    if (runtimes.Any(runtime => runtime.Channel == "Canary"))
                    {
                        Environment.SetEnvironmentVariable("WEBVIEW2_RELEASE_CHANNEL_PREFERENCE", "1", EnvironmentVariableTarget.User);
                        AppState.GetAppOverrideList().FromSystem();
                        return true;
                    }

                    const string canaryLink = "https://go.microsoft.com/fwlink/?linkid=2084649&Channel=Canary&language=en";
                    if (MessageBox.Show(
                        $"Before turning on this feature you need to have Canary installed from {canaryLink}. Do you want to install it now?",
                        "Canary missing", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                    {
                        System.Diagnostics.Process.Start(canaryLink);
                    }

                    return false;
                },
                () =>
                {
                    // Turn off:
                    Environment.SetEnvironmentVariable("WEBVIEW2_RELEASE_CHANNEL_PREFERENCE", null, EnvironmentVariableTarget.User);
                    AppState.GetAppOverrideList().FromSystem();
                },
                () =>
                {
                    var list = AppState.GetRuntimeList();
                    string val = Environment.GetEnvironmentVariable("WEBVIEW2_RELEASE_CHANNEL_PREFERENCE", EnvironmentVariableTarget.User);
                    return val != null && val == "1";
                }
                )
            {
                Name = "Canary self-hosting",
                Description = "Host apps use canary WebView2 runtime if installed instead of stable."
            });

            // Visual Hosting
            Items.Add(new EnvVarExperimentalFeature(
                "COREWEBVIEW2_FORCED_HOSTING_MODE",
                "COREWEBVIEW2_HOSTING_MODE_WINDOW_TO_VISUAL",
                null)
            {
                Name = "Visual hosting",
                Description = "Host apps use visual hosting instead of regular window hosting."
            });

            // To add more experimental features to the runtimes either:
            // add EnvVarExperimentalFeature if the feature is controlled by an enviroment variable
            // OR
            // add ExperimentalFeature with on, off and check delegates if the feature requires more specific operations
        }
    }
}